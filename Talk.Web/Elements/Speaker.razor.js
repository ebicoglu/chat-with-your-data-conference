export async function start(dotNetRef) {
    await navigator.mediaDevices.getUserMedia({ video: false, audio: { sampleRate: 24000 } });
    const audioCtx = new AudioContext({ sampleRate: 24000 });
    const pendingSources = [];
    let currentPlaybackEndTime = 0;
    let isPlaying = false;

    function setPlaying(value) {
        if (isPlaying === value) {
            return;
        }
        isPlaying = value;
        dotNetRef?.invokeMethodAsync("NotifyPlaybackState", value);
    }

    return {
        enqueue(data) {
            const bufferSource = toAudioBufferSource(audioCtx, data);
            pendingSources.push(bufferSource);
            bufferSource.onended = () => {
                const index = pendingSources.indexOf(bufferSource);
                if (index !== -1) {
                    pendingSources.splice(index, 1);
                }
                if (pendingSources.length === 0) {
                    currentPlaybackEndTime = 0;
                    setPlaying(false);
                }
            };
            currentPlaybackEndTime = Math.max(currentPlaybackEndTime, audioCtx.currentTime);
            bufferSource.start(currentPlaybackEndTime);
            currentPlaybackEndTime += bufferSource.buffer.duration;
            setPlaying(true);
        },

        clear() {
            pendingSources.forEach(source => {
                source.onended = null;
                source.stop();
            });
            pendingSources.length = 0;
            currentPlaybackEndTime = 0;
            setPlaying(false);
        }
    };
}

function toAudioBufferSource(audioCtx, data) {
    // We get int16, but need float32
    const int16Samples = new Int16Array(data.buffer.slice(data.byteOffset, data.byteOffset + data.byteLength));
    const numSamples = int16Samples.length;
    const float32Samples = new Float32Array(numSamples);
    for (let i = 0; i < numSamples; i++) {
        float32Samples[i] = int16Samples[i] / 0x7FFF;
    }
    const audioBuffer = new AudioBuffer({
        length: numSamples,
        sampleRate: audioCtx.sampleRate,
    });

    audioBuffer.copyToChannel(float32Samples, 0, 0);

    const bufferSource = audioCtx.createBufferSource();
    bufferSource.buffer = audioBuffer;
    bufferSource.connect(audioCtx.destination);
    return bufferSource;
}
