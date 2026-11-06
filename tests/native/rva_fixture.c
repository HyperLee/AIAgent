/* Trusted test-only scenarios. Never installed or included in product publish. */
#include <sys/types.h>
#include <sys/stat.h>
#include <sys/socket.h>
#include <sys/un.h>
#include <sys/proc_info.h>
#include <netinet/in.h>
#include <libproc.h>
#include <dirent.h>
#include <errno.h>
#include <fcntl.h>
#include <limits.h>
#include <signal.h>
#include <stdint.h>
#include <stddef.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>
#include <unistd.h>

static char root[PATH_MAX];
static volatile sig_atomic_t terminated;
static int64_t deadline;
static pid_t origin;

static int64_t now_ms(void) {
    struct timespec value;
    if (clock_gettime(CLOCK_MONOTONIC, &value)) _exit(90);
    return (int64_t)value.tv_sec * 1000 + value.tv_nsec / 1000000;
}
static void tick(void) {
    struct timespec value = {0, 20000000};
    nanosleep(&value, NULL);
}
static void path_for(char *path, const char *name) {
    if (snprintf(path, PATH_MAX, "%s/obj/rva/%s", root, name) >= PATH_MAX) _exit(90);
}
static void mark_value(const char *name, const char *value) {
    char path[PATH_MAX]; path_for(path, name);
    int fd = open(path, O_WRONLY | O_CREAT | O_EXCL | O_NOFOLLOW, 0600);
    if (fd < 0) { if (errno == EEXIST) return; _exit(90); }
    size_t length = strlen(value);
    if (write(fd, value, length) != (ssize_t)length) _exit(90);
    close(fd);
}
static void mark(const char *name) { mark_value(name, "1\n"); }
static int gate(const char *name) {
    char path[PATH_MAX]; struct stat info; path_for(path, name);
    return !lstat(path, &info) && S_ISREG(info.st_mode) && info.st_uid == getuid();
}
static int identity(pid_t pid, struct proc_bsdinfo *info) {
    return proc_pidinfo(pid, PROC_PIDTBSDINFO, 0, info, sizeof(*info)) == sizeof(*info);
}
static void register_self(void) {
    struct proc_bsdinfo info; char name[64], value[128], temporary[PATH_MAX], final[PATH_MAX];
    if (!identity(getpid(), &info)) _exit(90);
    snprintf(name, sizeof(name), "pid-%d", getpid());
    snprintf(value, sizeof(value), "%d %llu %llu\n", getpid(),
        (unsigned long long)info.pbi_start_tvsec, (unsigned long long)info.pbi_start_tvusec);
    path_for(final, name);
    snprintf(name, sizeof(name), ".registration-%d-%08x", getpid(), arc4random()); path_for(temporary, name);
    int fd = open(temporary, O_WRONLY | O_CREAT | O_EXCL | O_NOFOLLOW, 0600);
    if (fd < 0) { if (errno == EEXIST) return; _exit(90); }
    size_t length = strlen(value);
    if (write(fd, value, length) != (ssize_t)length || close(fd) || rename(temporary, final)) _exit(90);
}
static void on_term(int signal_number) { (void)signal_number; terminated = 1; }
static void wait_gate(const char *name) {
    while (!gate(name) && now_ms() < deadline && !terminated) tick();
    if (!gate(name)) _exit(91);
}
static void child_loop(int ignore_term, int output, int change_group) {
    register_self();
    if (getpid() != origin) mark("child-ready");
    if (change_group == 1) {
        wait_gate("change-group");
        if (setsid() < 0) _exit(91);
        mark("group-changed");
    }
    int emitted = 0;
    while (now_ms() < deadline) {
        if (terminated) {
            if (change_group == 2) {
                if (setsid() < 0) _exit(91);
                change_group = 0; mark("group-changed");
            }
            mark("term"); if (!ignore_term) _exit(0);
        }
        if (output && emitted++ % 5 == 0) {
            static const char message[] = "RVA_PIPE\n";
            (void)write(STDOUT_FILENO, message, sizeof(message) - 1);
        }
        tick();
    }
    _exit(0);
}
static void spawn_child(int ignore_term, int output, int change_group) {
    pid_t child = fork();
    if (child < 0) { mark("resource-blocked"); _exit(92); }
    if (!child) child_loop(ignore_term, output, change_group);
}
static int visit_owned(int signal_number, int *uncertain) {
    char directory[PATH_MAX]; path_for(directory, "");
    DIR *entries = opendir(directory); if (!entries) { *uncertain = 1; return 0; }
    struct dirent *entry; int alive = 0;
    while ((entry = readdir(entries))) {
        if (strncmp(entry->d_name, "pid-", 4)) continue;
        char *end = NULL; long named_pid = strtol(entry->d_name + 4, &end, 10);
        if (!end || *end || named_pid <= 1 || named_pid > INT_MAX) continue;
        char path[PATH_MAX]; path_for(path, entry->d_name);
        int fd = open(path, O_RDONLY | O_NOFOLLOW | O_NONBLOCK);
        if (fd < 0) { *uncertain = 1; continue; }
        char data[128] = {0}; struct stat state;
        int valid = !fstat(fd, &state) && S_ISREG(state.st_mode) && state.st_uid == getuid() && state.st_size < 127;
        ssize_t count = valid ? read(fd, data, sizeof(data) - 1) : -1; close(fd);
        int pid = 0; unsigned long long seconds = 0, micros = 0;
        if (count <= 0 || sscanf(data, "%d %llu %llu", &pid, &seconds, &micros) != 3 || pid != named_pid || pid == getpid()) {
            *uncertain = 1; continue;
        }
        struct proc_bsdinfo info;
        errno = 0;
        if (!identity(pid, &info)) {
            if (errno != ESRCH && errno != ENOENT) *uncertain = 1;
            continue;
        }
        if (info.pbi_start_tvsec == seconds && info.pbi_start_tvusec == micros && info.pbi_status != 5) {
            alive++;
            if (signal_number) {
                /* Revalidate immediately before signaling; never use names or process groups. */
                struct proc_bsdinfo current;
                errno = 0;
                int inspected = identity(pid, &current);
                if (!inspected) { if (errno != ESRCH && errno != ENOENT) *uncertain = 1; }
                else if (current.pbi_start_tvsec == seconds && current.pbi_start_tvusec == micros) {
                    if (kill(pid, signal_number) && errno != ESRCH) *uncertain = 1;
                }
            }
        }
    }
    closedir(entries); return alive;
}
static int watchdog(int limit) {
    deadline = now_ms() + 30000;
    mark("watchdog-ready"); wait_gate("start");
    deadline = now_ms() + limit;
    char value[64]; snprintf(value, sizeof(value), "%lld\n", (long long)deadline);
    mark_value("deadline", value); mark("armed");
    while (!gate("finish") && now_ms() < deadline) tick();
    if (!gate("finish")) mark("watchdog-cutoff");
    int rescued = 0, uncertain = 0, count = visit_owned(0, &uncertain);
    /* Rescan during cleanup too: a registered parent may have been spawning. */
    int64_t until = now_ms() + 2000;
    while ((count > 0 || uncertain) && now_ms() < until) {
        if (count > 0) rescued = 1;
        (void)visit_owned(SIGTERM, &uncertain); tick(); count = visit_owned(0, &uncertain);
    }
    until = now_ms() + 3000;
    while ((count > 0 || uncertain) && now_ms() < until) {
        if (count > 0) rescued = 1;
        (void)visit_owned(SIGKILL, &uncertain); tick(); count = visit_owned(0, &uncertain);
    }
    if (count != 0 || uncertain) { mark("guard-unconfirmed"); return 2; }
    mark(rescued ? "guard-reaped" : "guard-clean");
    return rescued;
}
static int denied_socket(int family, const struct sockaddr *address, socklen_t size) {
    int fd = socket(family, SOCK_STREAM, 0);
    if (fd < 0) return errno == EPERM || errno == EACCES;
    int result = connect(fd, address, size), reason = errno; close(fd);
    return result < 0 && (reason == EPERM || reason == EACCES);
}
static int policy(void) {
    mark("policy-read");
    int fd = open("Program.cs", O_RDONLY | O_NOFOLLOW);
    if (fd < 0) return 93; close(fd);
    mark("policy-source-write");
    fd = open("Program.cs", O_WRONLY | O_APPEND | O_NOFOLLOW);
    if (fd >= 0) { close(fd); return 93; }
    mark("policy-secret-read");
    fd = open("secrets/rva-canary", O_RDONLY | O_NOFOLLOW);
    if (fd >= 0) { close(fd); return 93; }
    mark("policy-outputs");
    for (int i = 0; i < 3; i++) {
        char path[PATH_MAX];
        if (i < 2) snprintf(path, sizeof(path), "%s/rva-output", i ? "obj" : "bin");
        else snprintf(path, sizeof(path), "%s/rva-output", getenv("TMPDIR"));
        fd = open(path, O_WRONLY | O_CREAT | O_EXCL | O_NOFOLLOW, 0600);
        if (fd < 0) return 93;
        if (write(fd, "1", 1) != 1) { close(fd); return 93; } close(fd);
    }
    mark("policy-network");
    struct sockaddr_in ipv4 = {0}; ipv4.sin_family = AF_INET; ipv4.sin_port = htons(9); ipv4.sin_addr.s_addr = htonl(INADDR_LOOPBACK);
    struct sockaddr_in6 ipv6 = {0}; ipv6.sin6_family = AF_INET6; ipv6.sin6_port = htons(9); ipv6.sin6_addr = in6addr_loopback;
    if (!denied_socket(AF_INET, (struct sockaddr *)&ipv4, sizeof(ipv4)) ||
        !denied_socket(AF_INET6, (struct sockaddr *)&ipv6, sizeof(ipv6))) return 93;
    /* UDP connect sends no packet. Port 53 tests DNS transport without a query. */
    ipv4.sin_port = htons(53);
    fd = socket(AF_INET, SOCK_DGRAM, 0);
    if (fd >= 0) {
        int result = connect(fd, (struct sockaddr *)&ipv4, sizeof(ipv4)), reason = errno;
        close(fd);
        if (!(result < 0 && (reason == EPERM || reason == EACCES))) return 93;
    } else if (errno != EPERM && errno != EACCES) return 93;
    mark("policy-dns-transport");
    mark("policy-outside-ipc");
    struct sockaddr_un outside = {0}; outside.sun_family = AF_UNIX;
    if (snprintf(outside.sun_path, sizeof(outside.sun_path), "%s-outside.sock", root) >= (int)sizeof(outside.sun_path)) return 93;
    if (!denied_socket(AF_UNIX, (struct sockaddr *)&outside, sizeof(outside))) return 93;
    struct stat private_state;
    if (stat(getenv("TMPDIR"), &private_state) || private_state.st_uid != getuid() ||
        (private_state.st_mode & 0777) != 0700) return 93;
    struct sockaddr_un inside = {0}; inside.sun_family = AF_UNIX;
    if (snprintf(inside.sun_path, sizeof(inside.sun_path), "%srva.sock", getenv("TMPDIR")) >= (int)sizeof(inside.sun_path)) return 93;
    socklen_t length = (socklen_t)(offsetof(struct sockaddr_un, sun_path) + strlen(inside.sun_path) + 1);
    inside.sun_len = (unsigned char)length;
    fd = socket(AF_UNIX, SOCK_STREAM, 0);
    if (fd < 0 || bind(fd, (struct sockaddr *)&inside, length)) return 93;
    mark("policy-private-ipc");
    int second = socket(AF_UNIX, SOCK_STREAM, 0);
    if (second < 0) { close(fd); return 93; }
    int collision = bind(second, (struct sockaddr *)&inside, length), reason = errno;
    close(second); close(fd);
    if (collision != -1 || reason != EADDRINUSE) return 93;
    mark("policy-private-collision");
    mark("policy-passed"); return 0;
}
static int race(void) {
    mark("ready"); wait_gate("race");
    if (!rename("bin", "bin-moved") || !rename("obj", "obj-moved")) return 94;
    if (!symlink("../Program.cs", "bin/alias") || !link("Program.cs", "bin/source-alias") ||
        !link("secrets/rva-canary", "bin/sensitive-alias") ||
        !link("Program.cs", "bin/PROGRAM.CS") || !link("Program.cs", "bin/unicode-e\xcc\x81.cs")) return 94;
    int fd = open("Program.cs", O_WRONLY | O_APPEND | O_NOFOLLOW);
    if (fd >= 0) { close(fd); return 94; }
    mark("race-passed"); return 0;
}
int main(int argc, char **argv) {
    origin = getpid();
    signal(SIGTERM, on_term); signal(SIGPIPE, SIG_IGN);
    if (argc == 4 && !strcmp(argv[1], "watchdog")) {
        int limit = atoi(argv[3]);
        if (limit != 30000 && limit != 60000 && limit != 150000) return 64;
        if (strlen(argv[2]) >= sizeof(root)) return 64;
        strcpy(root, argv[2]);
        struct stat info;
        if (lstat(root, &info) || !S_ISDIR(info.st_mode) || info.st_uid != getuid() || (info.st_mode & 0777) != 0700) return 64;
        return watchdog(limit);
    }
    if (argc < 2 || strcmp(argv[1], "build") || !getcwd(root, sizeof(root))) return 64;
    char path[PATH_MAX], scenario[32] = {0}, value[64] = {0};
    path_for(path, "scenario"); int fd = open(path, O_RDONLY | O_NOFOLLOW);
    if (fd < 0 || read(fd, scenario, sizeof(scenario) - 1) <= 0) return 64; close(fd);
    path_for(path, "deadline"); fd = open(path, O_RDONLY | O_NOFOLLOW);
    if (fd < 0 || read(fd, value, sizeof(value) - 1) <= 0) return 64; close(fd);
    deadline = strtoll(value, NULL, 10); if (deadline <= now_ms()) return 64;
    register_self();
    if (!strcmp(scenario, "success") || !strcmp(scenario, "invalid")) { mark("ready"); return 0; }
    if (!strcmp(scenario, "policy")) return policy();
    if (!strcmp(scenario, "race")) return race();
    if (!strcmp(scenario, "count")) {
        for (int i = 0; i < 254; i++) spawn_child(0, 0, 0);
        mark("ready"); wait_gate("grow256"); spawn_child(0, 0, 0);
        wait_gate("grow257"); spawn_child(0, 0, 0); mark("created257");
    } else if (!strcmp(scenario, "detach")) {
        pid_t child = fork(); if (child < 0) return 92;
        if (!child) {
            register_self(); if (setsid() < 0) _exit(92);
            pid_t grandchild = fork(); if (grandchild < 0) _exit(92);
            if (grandchild) _exit(0);
            register_self(); mark("detached"); child_loop(0, 0, 0);
        }
        wait_gate("detached"); mark("ready"); return 0;
    } else if (!strcmp(scenario, "hold-pipe") || !strcmp(scenario, "pipe-output")) {
        spawn_child(0, !strcmp(scenario, "pipe-output"), 0); mark("ready"); wait_gate("release-root"); return 0;
    } else if (!strcmp(scenario, "burst")) {
        mark("ready"); wait_gate("burst");
        for (int i = 0; i < 255; i++) spawn_child(0, 0, 0);
    } else if (!strcmp(scenario, "group-on-term")) {
        spawn_child(1, 0, 2); mark("ready");
    } else if (!strcmp(scenario, "group")) {
        spawn_child(0, 0, 1); mark("ready");
    } else if (!strcmp(scenario, "ignore-term")) {
        spawn_child(1, 0, 0); mark("ready"); child_loop(1, 0, 0);
    } else if (!strcmp(scenario, "wait")) {
        mark("ready");
    } else return 64;
    child_loop(0, 0, 0);
    return 0;
}
