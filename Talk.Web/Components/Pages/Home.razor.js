export async function start(componentInstance) {
    try {
        const micStream = await navigator.mediaDevices.getUserMedia({ video: false, audio: { sampleRate: 16000 } });
        processMicrophoneData(micStream, componentInstance);
        return micStream;
    } catch (ex) {
        alert(`Unable to access microphone: ${ex.toString()}`);
    }
}

export function setMute(micStream, mute) {
    if (micStream) {
        micStream.isMuted = mute;
    }
}

export async function renderChart(elementId, vegaSpecJson) {
    const el = document.getElementById(elementId);
    if (!el) return;

    if (typeof vegaEmbed === 'undefined') {
        el.innerHTML = '<div class="text-red-600 text-sm">vega-embed not loaded.</div>';
        return;
    }

    try {
        const spec = typeof vegaSpecJson === 'string' ? JSON.parse(vegaSpecJson) : vegaSpecJson;
        el.innerHTML = '';
        await vegaEmbed('#' + elementId, spec, {
            actions: { export: true, source: false, compiled: false, editor: false }
        });
    } catch (e) {
        el.innerHTML = `<div class="text-red-600 text-sm">Chart render error: ${e && e.message ? e.message : e}</div>`;
    }
}

export function downloadFile(fileName, contentType, bytes) {
    const blob = new Blob([bytes], { type: contentType });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);
    setTimeout(() => URL.revokeObjectURL(url), 1000);
}

async function processMicrophoneData(micStream, componentInstance) {
    const audioCtx = new AudioContext({ sampleRate: 24000 });
    const micStreamSource = audioCtx.createMediaStreamSource(micStream);

    const workletBlobUrl = URL.createObjectURL(new Blob([`
        registerProcessor('test', class param extends AudioWorkletProcessor {
            constructor() { super(); }
            process(input, output, parameters) {
              this.port.postMessage(input[0]);
              return true;
            }
          });
        `],
        { type: 'application/javascript' }));
    await audioCtx.audioWorklet.addModule(workletBlobUrl);
    const workletNode = new AudioWorkletNode(audioCtx, 'test', {});
    micStreamSource.connect(workletNode);
    workletNode.port.onmessage = async (e) => {
        if (micStream.isMuted) {
            return;
        }

        // We get float32, but need int16
        const float32Samples = e.data[0];
        const numSamples = float32Samples.length;
        const int16Samples = new Int16Array(numSamples);
        for (let i = 0; i < numSamples; i++) {
            int16Samples[i] = float32Samples[i] * 0x7FFF;
        }
        await componentInstance.invokeMethodAsync('ReceiveAudioDataAsync', new Uint8Array(int16Samples.buffer));
    }

    await componentInstance.invokeMethodAsync('OnMicConnectedAsync');
}
