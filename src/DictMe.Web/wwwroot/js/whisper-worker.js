// Exécute Whisper dans le navigateur (transformers.js + ONNX Runtime WebAssembly), hors du fil principal
// pour que l'interface reste fluide. Les modèles sont mis en cache par le navigateur après le premier
// téléchargement : la transcription fonctionne ensuite hors ligne.
// Version 3.x : ses modèles Whisper quantifiés se chargent correctement dans Safari, contrairement aux
// versions récentes d'ONNX Runtime (erreur « TransposeDQWeightsForMatMulNBits Missing required scale »).
import { pipeline, env } from 'https://cdn.jsdelivr.net/npm/@huggingface/transformers@3.8.1';

env.allowLocalModels = false;

let loaded = { model: null, pipe: null };

function progressReporter(id) {
    // Avancement global calculé sur l'ensemble des fichiers du modèle (encodeur, décodeur, tokenizer...).
    const files = {};
    return info => {
        if (info.status !== 'progress' || !info.total)
            return;
        files[info.file] = { loaded: info.loaded, total: info.total };
        const all = Object.values(files);
        const total = all.reduce((sum, f) => sum + f.total, 0);
        const done = all.reduce((sum, f) => sum + f.loaded, 0);
        postMessage({ type: 'progress', id, progress: total > 0 ? done / total * 100 : 0 });
    };
}

function createPipeline(model, id, sessionOptions) {
    return pipeline('automatic-speech-recognition', model, {
        device: 'wasm',
        dtype: 'q8',
        progress_callback: progressReporter(id),
        ...(sessionOptions ? { session_options: sessionOptions } : {}),
    });
}

async function getPipeline(model, id) {
    if (loaded.model === model)
        return loaded.pipe;

    if (loaded.pipe)
        await loaded.pipe.dispose?.();
    loaded = { model: null, pipe: null };

    let pipe;
    try {
        pipe = await createPipeline(model, id);
    } catch (error) {
        // Certaines optimisations du graphe ONNX échouent sur les modèles quantifiés : on réessaie sans.
        // Les fichiers sont déjà en cache, il n'y a rien à retélécharger.
        console.warn('DictMe : nouvel essai sans optimisation du graphe', error);
        pipe = await createPipeline(model, id, { graphOptimizationLevel: 'disabled' });
    }
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
