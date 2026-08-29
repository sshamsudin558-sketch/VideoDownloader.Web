const videoUrlInput = document.getElementById("videoUrl");
const getVideoBtn = document.getElementById("getVideoBtn");
const downloadBtn = document.getElementById("downloadBtn");

const errorMessage = document.getElementById("errorMessage");
const videoResult = document.getElementById("videoResult");

const thumbnail = document.getElementById("thumbnail");
const videoTitle = document.getElementById("videoTitle");
const videoDuration = document.getElementById("videoDuration");
const formatSelect = document.getElementById("formatSelect");

const downloadProgress = document.getElementById("downloadProgress");
const downloadStatus = document.getElementById("downloadStatus");
const downloadPercent = document.getElementById("downloadPercent");
const progressFill = document.getElementById("progressFill");
const downloadFileName = document.getElementById("downloadFileName");
const downloadFileSize = document.getElementById("downloadFileSize");

const downloadComplete = document.getElementById("downloadComplete");
const completedFileName = document.getElementById("completedFileName");

let currentVideoUrl = "";

/* =========================================================
   Utility
========================================================= */

function showError(message) {
    errorMessage.textContent = message || "Something went wrong.";
}

function clearError() {
    errorMessage.textContent = "";
}

function hideDownloadProgress() {
    if (downloadProgress) {
        downloadProgress.classList.add("hidden");
    }
}

function showDownloadProgress() {
    if (downloadProgress) {
        downloadProgress.classList.remove("hidden");
    }
}

function hideDownloadComplete() {
    if (downloadComplete) {
        downloadComplete.classList.add("hidden");
    }
}

function showDownloadComplete(fileName) {
    if (!downloadComplete) {
        return;
    }

    downloadComplete.classList.remove("hidden");

    if (completedFileName) {
        completedFileName.textContent = fileName || "Your video has been downloaded.";
    }
}

function resetProgress() {
    if (downloadStatus) {
        downloadStatus.textContent = "Preparing download...";
    }

    if (downloadPercent) {
        downloadPercent.textContent = "0%";
    }

    if (progressFill) {
        progressFill.style.width = "0%";
    }

    if (downloadFileName) {
        downloadFileName.textContent = "Preparing file...";
    }

    if (downloadFileSize) {
        downloadFileSize.textContent = "0 MB";
    }
}

function setProgress(percent, status) {
    const safePercent = Math.max(0, Math.min(100, Math.round(percent)));

    if (downloadPercent) {
        downloadPercent.textContent = `${ safePercent }%`;
    }

    if (progressFill) {
        progressFill.style.width = `${ safePercent }%`;
    }

    if (downloadStatus && status) {
        downloadStatus.textContent = status;
    }
}

function formatBytes(bytes) {
    if (!Number.isFinite(bytes) || bytes <= 0) {
        return "0 MB";
    }

    const mb = bytes / (1024 * 1024);

    if (mb < 1024) {
        return `${ mb.toFixed(1) } MB`;
    }

    const gb = mb / 1024;

    return `${ gb.toFixed(2) } GB`;
}

function formatDuration(seconds) {
    if (
        seconds === null ||
        seconds === undefined ||
        isNaN(seconds) ||
        Number(seconds) <= 0
    ) {
        return "Duration unavailable";
    }

    seconds = Math.floor(Number(seconds));

    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    const remainingSeconds = seconds % 60;

    if (hours > 0) {
        return (
            `${ hours }:` +
                `${ String(minutes).padStart(2, "0") }:`+
                    `${ String(remainingSeconds).padStart(2, "0") }`
        );
    }

    return `${ minutes }:${ String(remainingSeconds).padStart(2, "0") }`;
}

/* =========================================================
   API Error Reader
========================================================= */

async function readApiError(response) {
    const responseText = await response.text();

    if (!responseText) {
        return `Request failed (${response.status}).`;
    }

    try {
        const data = JSON.parse(responseText);

        return data.message || data.error || `Request failed (${response.status}).`;
    } catch {
        return responseText;
    }
}

/* =========================================================
   Get Video Information
========================================================= */

async function getVideoInfo() {
    const url = videoUrlInput.value.trim();

    clearError();

    videoResult.classList.add("hidden");

    hideDownloadProgress();
    hideDownloadComplete();
    resetProgress();

    if (!url) {
        showError("Please enter a video URL.");
        videoUrlInput.focus();
        return;
    }

    let parsedUrl;

    try {
        parsedUrl = new URL(url);
    } catch {
        showError("Please enter a valid video URL.");
        return;
    }

    if (parsedUrl.protocol !== "http:" && parsedUrl.protocol !== "https:") {
        showError("Only HTTP and HTTPS URLs are supported.");
        return;
    }

    getVideoBtn.disabled = true;
    getVideoBtn.textContent = "Loading...";

    try {
        const response = await fetch("/api/Download/info", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({ url: url })
        });

        if (!response.ok) {
            const message = await readApiError(response);
            throw new Error(message);
        }

        const data = await response.json();

        if (!data) {
            throw new Error("The server returned an empty response.");
        }

        if (
            !data.availableFormats ||
            !Array.isArray(data.availableFormats) ||
            data.availableFormats.length === 0
        ) {
            throw new Error("No downloadable formats were found.");
        }

        currentVideoUrl = url;

        /* Thumbnail */
        if (data.thumbnail) {
            thumbnail.src = data.thumbnail;
            thumbnail.style.display = "block";
        } else {
            thumbnail.removeAttribute("src");
            thumbnail.style.display = "none";
        }

        /* Title */
        videoTitle.textContent = data.title || "Unknown Title";

        /* Duration */
        videoDuration.textContent = formatDuration(data.durationSeconds);

        /* Clear formats */
        formatSelect.innerHTML = "";

        /* Add formats */
        const formats = data.availableFormats;
        const addedFormats = new Set();

        formats.forEach(function (format) {
            if (!format || !format.formatId) {
                return;
            }

            const formatId = String(format.formatId);

            if (addedFormats.has(formatId)) {
                return;
            }

            addedFormats.add(formatId);

            const option = document.createElement("option");
            option.value = formatId;

            const extension = format.extension
                ? String(format.extension).toUpperCase()
                : "FILE";

            if (format.isAudioOnly) {
                option.textContent = `Audio Only - ${ extension }`;
            } else {
                const resolution = format.resolution || "Unknown Quality";
                option.textContent = `${ resolution } - ${ extension }`;
            }

            formatSelect.appendChild(option);
        });

        if (formatSelect.options.length === 0) {
            throw new Error("No usable download formats were found.");
        }

        videoResult.classList.remove("hidden");

        videoResult.scrollIntoView({
            behavior: "smooth",
            block: "start"
        });
    } catch (error) {
        console.error("Get video info error:", error);

        currentVideoUrl = "";

        showError(error.message || "Could not retrieve video information.");
    } finally {
        getVideoBtn.disabled = false;
        getVideoBtn.textContent = "Get Video";
    }
}

/* =========================================================
   Download Video
========================================================= */

async function downloadVideo() {
    clearError();
    hideDownloadComplete();

    if (!currentVideoUrl) {
        showError("Please get video information first.");
        return;
    }

    const formatId = formatSelect.value;

    if (!formatId) {
        showError("Please select a format.");
        return;
    }

    downloadBtn.disabled = true;
    downloadBtn.textContent = "Preparing...";

    resetProgress();
    showDownloadProgress();

    setProgress(5, "Preparing download...");

    try {
        const response = await fetch("/api/Download", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                url: currentVideoUrl,
                formatId: formatId
            })
        });

        if (!response.ok) {
            const message = await readApiError(response);
            throw new Error(`Download failed(${ response.status }): ${ message }`);
        }

        setProgress(25, "Receiving video...");

        downloadBtn.textContent = "Downloading...";

        const contentLength = response.headers.get("Content-Length");
        const total = contentLength ? Number(contentLength) : 0;

        const contentDisposition = response.headers.get("Content-Disposition");

        let fileName = "video-download.mp4";

        if (contentDisposition) {
            const utf8Match = contentDisposition.match(/filename\*=UTF-8''([^;]+)/i);

            if (utf8Match) {
                try {
                    fileName = decodeURIComponent(utf8Match[1]);
                } catch {
                    fileName = utf8Match[1];
                }
            } else {
                const normalMatch = contentDisposition.match(/filename="?([^"]+)"?/i);

                if (normalMatch) {
                    fileName = normalMatch[1];
                }
            }
        }

        if (downloadFileName) {
            downloadFileName.textContent = fileName;
        }

        /* Read response stream */
        if (response.body && typeof response.body.getReader === "function") {
            const reader = response.body.getReader();
            const chunks = [];
            let received = 0;

            while (true) {
                const { done, value } = await reader.read();

                if (done) {
                    break;
                }

                if (value) {
                    chunks.push(value);
                    received += value.length;
                }

                if (downloadFileSize) {
                    downloadFileSize.textContent = formatBytes(received);
                }

                if (total > 0) {
                    const percent = (received / total) * 100;
                    setProgress(percent, "Downloading...");
                } else {
                    const estimatedPercent = Math.min(
                        95,
                        25 + (received / (1024 * 1024)) * 2
                    );
                    setProgress(estimatedPercent, "Downloading...");
                }
            }

            setProgress(100, "Download complete.");

            const blob = new Blob(chunks);

            if (blob.size === 0) {
                throw new Error("The downloaded file is empty.");
            }

            if (downloadFileSize) {
                downloadFileSize.textContent = formatBytes(blob.size);
            }

            const downloadUrl = window.URL.createObjectURL(blob);
            const link = document.createElement("a");

            link.href = downloadUrl;
            link.download = fileName;
            link.style.display = "none";

            document.body.appendChild(link);
            link.click();
            link.remove();

            setTimeout(function () {
                window.URL.revokeObjectURL(downloadUrl);
            }, 2000);
        } else {
            const blob = await response.blob();

            if (!blob || blob.size === 0) {
                throw new Error("The downloaded file is empty.");
            }

            setProgress(100, "Download complete.");

            if (downloadFileSize) {
                downloadFileSize.textContent = formatBytes(blob.size);
            }

            const downloadUrl = window.URL.createObjectURL(blob);
            const link = document.createElement("a");

            link.href = downloadUrl;
            link.download = fileName;

            document.body.appendChild(link);
            link.click();
            link.remove();

            setTimeout(function () {
                window.URL.revokeObjectURL(downloadUrl);
            }, 2000);
        }

        downloadBtn.textContent = "Download Complete";

        showDownloadComplete(fileName);

        setTimeout(function () {
            downloadBtn.textContent = "Download";
        }, 2500);
    } catch (error) {
        console.error("Download error:", error);

        showError(error.message || "Download failed.");

        setProgress(0, "Download failed.");
    } finally {
        downloadBtn.disabled = false;

        setTimeout(function () {
            downloadBtn.textContent = "Download";
        }, 3000);
    }
}

/* =========================================================
   Enter Key
========================================================= */

videoUrlInput.addEventListener("keydown", function (event) {
    if (event.key === "Enter") {
        event.preventDefault();
        getVideoInfo();
    }
});
