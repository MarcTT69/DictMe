// Pont entre Blazor et les API du navigateur : micro, décodage audio, presse-papiers, partage, stockage.

let dotnet = null;
let autoCopy = true;
let recorder = null;
let stream = null;
let chunks = [];
let audio = null;
let worker = null;
let nextRequestId = 1;
const requests = new Map();
let pendingClipboard = null;

const CACHE_NAME = 'transformers-cache';

export function init(dotnetRef) {
    dotnet = dotnetRef;

    // Le bouton micro est géré ici et non par Blazor : sur iPhone, Safari n'autorise l'écriture dans le
    // presse-papiers que pendant un geste de l'utilisateur. On « réserve » donc le presse-papiers au
    // moment du toucher qui arrête l'enregistrement, et on le remplit quand le texte est prêt.
    document.addEventListener('click', event => {
        const button = event.target.closest('[data-record-button]');
        if (!button || button.disabled)
            return;
        if (recorder?.state === 'recording' && autoCopy)
            reserveClipboard();
        dotnet.invokeMethodAsync('OnRecordTapped');
    });

    navigator.storage?.persist?.().catch(() => { });
}

export function configure(options) {
    autoCopy = !!options.autoCopy;
}

export function isStandalone() {
    return window.matchMedia('(display-mode: standalone)').matches || navigator.standalone === true;
}

export function isIos() {
    return /iPad|iPhone|iPod/.test(navigator.userAgent)
        || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
}

// ---------- Enregistrement ----------

export async function startRecording() {
    if (!navigator.mediaDevices?.getUserMedia || typeof MediaRecorder === 'undefined')
        return 'unsupported';

    try {
        stream = await navigator.mediaDevices.getUserMedia({
            audio: { channelCount: 1, echoCancellation: true, noiseSuppression: true },
        });
    } catch (error) {
        return error?.name === 'NotAllowedError' ? 'permission' : 'microphone';
    }

    const mimeType = ['audio/mp4', 'audio/webm;codecs=opus', 'audio/webm']
        .find(type => MediaRecorder.isTypeSupported?.(type));
    recorder = new MediaRecorder(stream, mimeType ? { mimeType } : undefined);
    chunks = [];
    audio = null;
    recorder.ondataavailable = event => {
        if (event.data.size > 0)
            chunks.push(event.data);
    };
    recorder.start(1000);
    return null;
}

/** Arrête l'enregistrement, convertit l'audio en mono 16 kHz et renvoie sa durée en secondes. */
export async function stopRecording() {
    if (!recorder)
        return 0;

    const current = recorder;
    recorder = null;
    const stopped = new Promise(resolve => current.addEventListener('stop', resolve, { once: true }));
    if (current.state !== 'inactive')
        current.stop();
    await stopped;
    stream?.getTracks().forEach(track => track.stop());
    stream = null;

    const blob = new Blob(chunks, { type: current.mimeType || 'audio/mp4' });
    chunks = [];
    audio = blob.size > 0 ? await decodeTo16kMono(blob) : new Float32Array(0);
    return audio.length / 16000;
}

async function decodeTo16kMono(blob) {
    const data = await blob.arrayBuffer();
    let decoded;
    try {
        // Un OfflineAudioContext à 16 kHz rééchantillonne directement au décodage.
        decoded = await new OfflineAudioContext(1, 1, 16000).decodeAudioData(data.slice(0));
    } catch {
        const Context = window.AudioContext || window.webkitAudioContext;
        const context = new Context();
        try {
            decoded = await context.decodeAudioData(data.slice(0));
        } finally {
            context.close?.();
        }
    }

    if (decoded.sampleRate !== 16000 || decoded.numberOfChannels !== 1) {
        const offline = new OfflineAudioContext(1, Math.ceil(decoded.duration * 16000), 16000);
        const source = offline.createBufferSource();
        source.buffer = decoded;
        source.connect(offline.destination);
        source.start();
        decoded = await offline.startRendering();
    }
    return decoded.getChannelData(0);
}

// ---------- Transcription ----------

function getWorker() {
    if (!worker) {
        worker = new Worker(new URL('./whisper-worker.js', import.meta.url), { type: 'module' });
        worker.addEventListener('message', ({ data }) => {
            const request = requests.get(data.id);
            if (!request)
                return;
            if (data.type === 'progress') {
                dotnet?.invokeMethodAsync('OnModelProgress', request.model, data.progress);
            } else {
                requests.delete(data.id);
                data.type === 'done' ? request.resolve(data.result) : request.reject(new Error(data.message));
            }
        });
        worker.addEventListener('error', event => {
            for (const request of requests.values())
                request.reject(new Error(event.message || 'Le moteur de transcription n\'a pas pu démarrer.'));
            requests.clear();
            worker = null;
        });
    }
    return worker;
}

function post(message, transfer = []) {
    const id = nextRequestId++;
    return new Promise((resolve, reject) => {
        requests.set(id, { resolve, reject, model: message.model });
        getWorker().postMessage({ ...message, id }, transfer);
    });
}

export function loadModel(model) {
    return post({ type: 'load', model });
}

export function transcribe(model, language) {
    if (!audio || audio.length === 0)
        return Promise.resolve([]);
    const samples = audio;
    audio = null;
    return post({ type: 'transcribe', model, language, audio: samples }, [samples.buffer]);
}

export async function cachedModels(models) {
    if (!('caches' in window))
        return [];
    const cache = await caches.open(CACHE_NAME);
    const urls = (await cache.keys()).map(request => request.url);
    return models.filter(model => urls.some(url => url.includes(`/${model}/resolve/`) && url.includes('decoder_model_merged')));
}

export async function deleteModel(model) {
    if (!('caches' in window))
        return;
    const cache = await caches.open(CACHE_NAME);
    for (const request of await cache.keys()) {
        if (request.url.includes(`/${model}/resolve/`))
            await cache.delete(request);
    }
    // Libère aussi la mémoire si ce modèle était chargé.
    worker?.terminate();
    worker = null;
}

// ---------- Presse-papiers et partage ----------

function reserveClipboard() {
    if (!window.ClipboardItem || !navigator.clipboard?.write)
        return;
    const text = new Promise((resolve, reject) => { pendingClipboard = { resolve, reject }; });
    pendingClipboard.write = navigator.clipboard
        .write([new ClipboardItem({ 'text/plain': text.then(value => new Blob([value], { type: 'text/plain' })) })]);
    pendingClipboard.write.catch(() => { });
}

/** Remplit le presse-papiers réservé au toucher (ou tente une copie directe). Renvoie true si c'est copié. */
export async function completeClipboard(text) {
    const pending = pendingClipboard;
    pendingClipboard = null;
    if (pending) {
        pending.resolve(text);
        try {
            await pending.write;
            return true;
        } catch {
            // On retente sans réservation ci-dessous (fonctionne sur la plupart des navigateurs de bureau).
        }
    }
    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch {
        return false;
    }
}

export function cancelClipboard() {
    pendingClipboard?.reject(new Error('cancelled'));
    pendingClipboard = null;
}

/** Appelée directement depuis un toucher sur « Copier ». */
export function copyText(text) {
    if (navigator.clipboard?.writeText) {
        navigator.clipboard.writeText(text).catch(() => legacyCopy(text));
        return true;
    }
    return legacyCopy(text);
}

function legacyCopy(text) {
    const area = document.createElement('textarea');
    area.value = text;
    area.setAttribute('readonly', '');
    area.style.position = 'fixed';
    area.style.opacity = '0';
    document.body.appendChild(area);
    area.select();
    area.setSelectionRange(0, text.length);
    const ok = document.execCommand('copy');
    area.remove();
    return ok;
}

/** Renvoie false si le partage n'est pas disponible (le texte est alors copié). */
export function share(text) {
    if (navigator.share) {
        navigator.share({ text }).catch(() => { });
        return true;
    }
    copyText(text);
    return false;
}

// ---------- Stockage local ----------

export function getItem(key) {
    try {
        return localStorage.getItem(key);
    } catch {
        return null;
    }
}

export function setItem(key, value) {
    try {
        localStorage.setItem(key, value);
    } catch {
        // Stockage plein ou désactivé (navigation privée) : l'app reste utilisable sans historique.
    }
}
