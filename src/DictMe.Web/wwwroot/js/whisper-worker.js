// Exécute Whisper dans le navigateur (transformers.js + ONNX Runtime WebAssembly), hors du fil principal
// pour que l'interface reste fluide. Les modèles sont mis en cache par le navigateur après le premier
// téléchargement : la transcription fonctionne ensuite hors ligne.
import { pipeline, env } from 'https://cdn.jsdelivr.net/npm/@huggingface/transformers@4.2.0';

env.allowLocalModels = false;

let loaded = { model: null, pipe: null };

async function getPipeline(model, id) {
    if (loaded.model === model)
        return loaded.pipe;

    if (loaded.pipe)
        await loaded.pipe.dispose?.();
    loaded = { model: null, pipe: null };

    const pipe = await pipeline('automatic-speech-recognition', model, {
        device: 'wasm',
        dtype: 'q8',
        progress_callback: info => {
            if (info.status === 'progress_total')
                postMessage({ type: 'progress', id, progress: info.progress });
        },
    });
    loaded = { model, pipe };
    return pipe;
}

self.addEventListener('message', async ({ data }) => {
    const { id, type } = data;
    try {
        const pipe = await getPipeline(data.model, id);
        if (type === 'load') {
            postMessage({ type: 'done', id, result: true });
            return;
        }

        const output = await pipe(data.audio, {
            language: data.language === 'auto' ? null : data.language,
            task: 'transcribe',
            chunk_length_s: 30,
            stride_length_s: 5,
            return_timestamps: true,
        });

        // Les segments séparés permettent d'écarter ceux que Whisper invente sur du silence.
        const segments = (output.chunks ?? []).map(c => c.text);
        if (segments.length === 0 && output.text)
            segments.push(output.text);
        postMessage({ type: 'done', id, result: segments });
    } catch (error) {
        postMessage({ type: 'error', id, message: error?.message ?? String(error) });
    }
});
