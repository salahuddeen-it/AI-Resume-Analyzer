(function () {
    "use strict";

    // ----- Drag & drop upload -----
    const dropzone = document.getElementById("dropzone");
    const fileInput = document.getElementById("ResumeFile");
    const fileName = document.getElementById("fileName");
    const allowed = [".pdf", ".docx"];
    const maxBytes = 5 * 1024 * 1024;

    function showFile(file) {
        if (!file) return;
        const ext = file.name.substring(file.name.lastIndexOf(".")).toLowerCase();
        if (!allowed.includes(ext)) {
            alert("Only PDF and DOCX files are allowed.");
            fileInput.value = "";
            return;
        }
        if (file.size > maxBytes) {
            alert("File size must not exceed 5 MB.");
            fileInput.value = "";
            return;
        }
        fileName.textContent = file.name + " (" + Math.max(1, Math.round(file.size / 1024)) + " KB)";
        dropzone.classList.add("has-file");
        dropzone.querySelector(".dropzone-icon i").className = "bi bi-file-earmark-check";
    }

    if (dropzone && fileInput) {
        fileInput.addEventListener("change", () => showFile(fileInput.files[0]));

        ["dragenter", "dragover"].forEach(evt =>
            dropzone.addEventListener(evt, e => { e.preventDefault(); dropzone.classList.add("dragover"); }));
        ["dragleave", "drop"].forEach(evt =>
            dropzone.addEventListener(evt, e => { e.preventDefault(); dropzone.classList.remove("dragover"); }));

        dropzone.addEventListener("drop", e => {
            if (e.dataTransfer.files.length) {
                fileInput.files = e.dataTransfer.files;
                showFile(fileInput.files[0]);
            }
        });
    }

    // ----- Loading overlay while analysing -----
    const form = document.getElementById("analyzeForm");
    if (form) {
        form.addEventListener("submit", e => {
            if (!fileInput.files.length) {
                e.preventDefault();
                dropzone.classList.add("dragover");
                setTimeout(() => dropzone.classList.remove("dragover"), 800);
                return;
            }
            const overlay = document.getElementById("loadingOverlay");
            const step = document.getElementById("loadingStep");
            const steps = ["Extracting text from your file", "Sending content to AI", "Analysing skills & experience",
                           "Comparing with job description", "Calculating your score", "Preparing suggestions"];
            let i = 0;
            overlay.classList.add("show");
            document.getElementById("analyzeBtn").disabled = true;
            setInterval(() => { i = (i + 1) % steps.length; step.textContent = steps[i]; }, 2200);
        });
    }

    // ----- Animate score counter & bars -----
    document.querySelectorAll(".score-value[data-count]").forEach(el => {
        const target = parseInt(el.dataset.count, 10) || 0;
        let current = 0;
        const stepSize = Math.max(1, Math.round(target / 40));
        const timer = setInterval(() => {
            current = Math.min(target, current + stepSize);
            el.textContent = current;
            if (current >= target) clearInterval(timer);
        }, 25);
    });

    setTimeout(() => {
        document.querySelectorAll(".animated-bar").forEach(bar => bar.style.width = bar.dataset.width + "%");
    }, 150);

    // ----- Auto-hide alerts -----
    document.querySelectorAll(".auto-dismiss").forEach(alertEl => {
        setTimeout(() => { alertEl.classList.add("hide"); setTimeout(() => alertEl.remove(), 400); }, 3500);
    });
})();
