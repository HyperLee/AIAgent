/* Test-only dyld interposition: loss is never a confirmed disappearance. */
#include <sys/types.h>
#include <libproc.h>
#include <errno.h>

static int denied_observation(int pid, int flavor, unsigned long long argument, void *buffer, int size) {
    (void)pid; (void)flavor; (void)argument; (void)buffer; (void)size;
    errno = EPERM;
    return 0;
}
__attribute__((used, section("__DATA,__interpose")))
static const struct { const void *replacement; const void *original; } interpose = {
    (const void *)denied_observation, (const void *)proc_pidinfo
};
