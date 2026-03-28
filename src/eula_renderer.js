window.addEventListener('DOMContentLoaded', async () => {
    const contentDiv = document.getElementById('eula-content');
    const statusText = document.getElementById('license-status');
    const modal = document.getElementById('code-modal');
    const modalInput = document.getElementById('modal-input');
    
    let activeCode = "";

    const loadLicense = async (code = "") => {
        try {
            // Request license data from main process
            const result = await window.api.invoke('get-eula-text', code);
            
            if (contentDiv) {
                contentDiv.innerText = result.content;
                contentDiv.scrollTop = 0; // Reset scroll
            }
            
            activeCode = code;

            if (statusText) {
                statusText.innerText = result.displayName;

                // Color logic: Green for special, Blue for standard
                if (result.isSpecial) {
                    statusText.style.color = "#4ade80"; // Green
                } else {
                    statusText.style.color = "#60a5fa"; // Blue
                }
            }
        } catch (err) {
            if (contentDiv) contentDiv.innerText = "Error: Could not load license.";
        }
    };

    // Initial load (Standard)
    loadLicense("");

    // --- Modal Logic ---
    const openBtn = document.getElementById('open-code-modal');
    const closeBtn = document.getElementById('close-modal');
    const confirmBtn = document.getElementById('confirm-code');

    if (openBtn) {
        openBtn.onclick = () => {
            modal.classList.remove('hidden');
            if (modalInput) modalInput.focus();
        };
    }

    if (closeBtn) {
        closeBtn.onclick = () => {
            modal.classList.add('hidden');
            if (modalInput) modalInput.value = ""; // Clear on close
        };
    }

    const submitCode = () => {
        const code = modalInput.value.trim();
        loadLicense(code); // Load whatever code they typed
        modal.classList.add('hidden');
        modalInput.value = "";
    };

    if (confirmBtn) confirmBtn.onclick = submitCode;

    if (modalInput) {
        modalInput.addEventListener('keypress', (e) => {
            if (e.key === 'Enter') submitCode();
        });
    }

    // --- Accept / Decline ---
    const acceptBtn = document.getElementById('accept-btn');
    if (acceptBtn) {
        acceptBtn.onclick = () => window.api.send('accept-eula', activeCode);
    }

    const declineBtn = document.getElementById('decline-btn');
    if (declineBtn) {
        declineBtn.onclick = () => window.api.send('decline-eula');
    }
});
