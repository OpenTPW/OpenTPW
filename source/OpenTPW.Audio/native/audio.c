/* Opaque ABI keeps minimp3 implementation details out of managed layouts. */
#include <stdlib.h>
#define MINIMP3_IMPLEMENTATION
#include "minimp3/minimp3.h"
#ifdef _WIN32
#define EXPORT __declspec(dllexport)
#else
#define EXPORT __attribute__((visibility("default")))
#endif
EXPORT void *otp_mpeg_create(void)
{
    mp3dec_t *decoder = (mp3dec_t *)calloc(1, sizeof(mp3dec_t));
    if (decoder) mp3dec_init(decoder);
    return decoder;
}
EXPORT void otp_mpeg_destroy(void *decoder) { free(decoder); }
EXPORT int otp_mpeg_decode(void *decoder, const uint8_t *input, int length,
    int16_t *output, int capacity, int *consumed, int *skipped, int *channels, int *rate, int *layer)
{
    mp3dec_frame_info_t info = {0};
    int samples;
    /* minimp3 may emit a full stereo frame, even when the caller expected mono. */
    if (!decoder || !input || length <= 0 || !output || capacity < MINIMP3_MAX_SAMPLES_PER_FRAME ||
        !consumed || !skipped || !channels || !rate || !layer) return -1;
    /* SDT clips can end with a complete MPEG frame followed by one or two padding bytes.
       Bound each non-free-format frame from its real header: minimp3's stream scanner
       otherwise requires a following sync and discards that valid last frame. */
    if (length >= 4 && hdr_valid(input)) {
        int frame_bytes = hdr_frame_bytes(input, 0) + hdr_padding(input);
        if (frame_bytes > 4 && frame_bytes <= length) length = frame_bytes;
    }
    samples = mp3dec_decode_frame((mp3dec_t *)decoder, input, length, output, &info);
    *consumed = info.frame_bytes; *skipped = info.frame_offset;
    *channels = info.channels; *rate = info.hz; *layer = info.layer;
    return samples;
}
