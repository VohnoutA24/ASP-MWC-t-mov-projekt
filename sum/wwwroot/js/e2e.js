/**
 * School Portal - End-to-End Encryption (E2E) Javascript Library
 * Powered by Web Crypto API
 */

const E2E = (function () {
    // Helper: Convert ArrayBuffer to Base64
    function arrayBufferToBase64(buffer) {
        let binary = '';
        const bytes = new Uint8Array(buffer);
        const len = bytes.byteLength;
        for (let i = 0; i < len; i++) {
            binary += String.fromCharCode(bytes[i]);
        }
        return window.btoa(binary);
    }

    // Helper: Convert Base64 to ArrayBuffer
    function base64ToArrayBuffer(base64) {
        const binary_string = window.atob(base64);
        const len = binary_string.length;
        const bytes = new Uint8Array(len);
        for (let i = 0; i < len; i++) {
            bytes[i] = binary_string.charCodeAt(i);
        }
        return bytes.buffer;
    }

    // Helper: String to UTF-8 ArrayBuffer
    function stringToArrayBuffer(str) {
        return new TextEncoder().encode(str);
    }

    // Helper: UTF-8 ArrayBuffer to String
    function arrayBufferToString(buffer) {
        return new TextDecoder().decode(buffer);
    }

    return {
        // Derive AES key from password and email (salt)
        async deriveKey(password, email) {
            const encoder = new TextEncoder();
            const passwordBuffer = encoder.encode(password);
            const salt = encoder.encode(email.toLowerCase().trim());
            
            const baseKey = await window.crypto.subtle.importKey(
                "raw",
                passwordBuffer,
                { name: "PBKDF2" },
                false,
                ["deriveBits", "deriveKey"]
            );
            
            return await window.crypto.subtle.deriveKey(
                {
                    name: "PBKDF2",
                    salt: salt,
                    iterations: 100000,
                    hash: "SHA-256"
                },
                baseKey,
                { name: "AES-GCM", length: 256 },
                false,
                ["encrypt", "decrypt"]
            );
        },

        // Generate RSA-OAEP 2048 key pair
        async generateKeyPair() {
            return await window.crypto.subtle.generateKey(
                {
                    name: "RSA-OAEP",
                    modulusLength: 2048,
                    publicExponent: new Uint8Array([1, 0, 1]),
                    hash: "SHA-256"
                },
                true,
                ["encrypt", "decrypt"]
            );
        },

        // Export Public Key to Base64 SPKI
        async exportPublicKey(publicKey) {
            const exported = await window.crypto.subtle.exportKey("spki", publicKey);
            return arrayBufferToBase64(exported);
        },

        // Import Public Key from Base64 SPKI
        async importPublicKey(base64Spki) {
            const buffer = base64ToArrayBuffer(base64Spki);
            return await window.crypto.subtle.importKey(
                "spki",
                buffer,
                {
                    name: "RSA-OAEP",
                    hash: "SHA-256"
                },
                true,
                ["encrypt"]
            );
        },

        // Export Private Key to encrypted Base64 PKCS#8
        async encryptPrivateKey(privateKey, aesKey) {
            const exported = await window.crypto.subtle.exportKey("pkcs8", privateKey);
            const iv = window.crypto.getRandomValues(new Uint8Array(12));
            const ciphertext = await window.crypto.subtle.encrypt(
                { name: "AES-GCM", iv: iv },
                aesKey,
                exported
            );
            return arrayBufferToBase64(iv) + ":" + arrayBufferToBase64(ciphertext);
        },

        // Import Private Key from encrypted Base64 PKCS#8
        async decryptPrivateKey(encryptedString, aesKey) {
            const parts = encryptedString.split(":");
            if (parts.length !== 2) throw new Error("Neplatný formát šifrovaného klíče.");
            
            const iv = new Uint8Array(base64ToArrayBuffer(parts[0]));
            const ciphertext = base64ToArrayBuffer(parts[1]);
            
            const decrypted = await window.crypto.subtle.decrypt(
                { name: "AES-GCM", iv: iv },
                aesKey,
                ciphertext
            );
            
            return await window.crypto.subtle.importKey(
                "pkcs8",
                decrypted,
                {
                    name: "RSA-OAEP",
                    hash: "SHA-256"
                },
                true,
                ["decrypt"]
            );
        },

        // Export Private Key to Base64 string directly (for SessionStorage)
        async exportPrivateKeyToSession(privateKey) {
            const exported = await window.crypto.subtle.exportKey("pkcs8", privateKey);
            return arrayBufferToBase64(exported);
        },

        // Import Private Key from Base64 string directly (from SessionStorage)
        async importPrivateKeyFromSession(base64Pkcs8) {
            const buffer = base64ToArrayBuffer(base64Pkcs8);
            return await window.crypto.subtle.importKey(
                "pkcs8",
                buffer,
                {
                    name: "RSA-OAEP",
                    hash: "SHA-256"
                },
                true,
                ["decrypt"]
            );
        },

        // Encrypt message (subject + body)
        async encryptMessage(subject, body, recipientPublicKeyObj, senderPublicKeyObj) {
            // Generate message key (AES-GCM 256)
            const aesKey = await window.crypto.subtle.generateKey(
                { name: "AES-GCM", length: 256 },
                true,
                ["encrypt", "decrypt"]
            );

            // Encrypt subject
            const subjectIv = window.crypto.getRandomValues(new Uint8Array(12));
            const subjectBytes = stringToArrayBuffer(subject);
            const subjectCipher = await window.crypto.subtle.encrypt(
                { name: "AES-GCM", iv: subjectIv },
                aesKey,
                subjectBytes
            );

            // Encrypt body
            const bodyIv = window.crypto.getRandomValues(new Uint8Array(12));
            const bodyBytes = stringToArrayBuffer(body);
            const bodyCipher = await window.crypto.subtle.encrypt(
                { name: "AES-GCM", iv: bodyIv },
                aesKey,
                bodyBytes
            );

            // Export AES Key to raw bytes
            const rawAesKey = await window.crypto.subtle.exportKey("raw", aesKey);

            // Encrypt AES key for recipient & sender
            const encKeyForRecipient = await window.crypto.subtle.encrypt(
                { name: "RSA-OAEP" },
                recipientPublicKeyObj,
                rawAesKey
            );

            const encKeyForSender = await window.crypto.subtle.encrypt(
                { name: "RSA-OAEP" },
                senderPublicKeyObj,
                rawAesKey
            );

            const payload = {
                version: "1",
                recipientKey: arrayBufferToBase64(encKeyForRecipient),
                senderKey: arrayBufferToBase64(encKeyForSender),
                subjectIv: arrayBufferToBase64(subjectIv),
                subjectCiphertext: arrayBufferToBase64(subjectCipher),
                bodyIv: arrayBufferToBase64(bodyIv),
                bodyCiphertext: arrayBufferToBase64(bodyCipher)
            };

            return "__E2E__:" + JSON.stringify(payload);
        },

        // Decrypt message (subject + body)
        async decryptMessage(payloadString, privateKeyObj, isSender) {
            if (!payloadString.startsWith("__E2E__:")) {
                return null; // Not E2E
            }

            const payload = JSON.parse(payloadString.substring(8));
            const encryptedAesKeyBase64 = isSender ? payload.senderKey : payload.recipientKey;
            if (!encryptedAesKeyBase64) {
                throw new Error("Šifrovací klíč pro tuto roli nebyl nalezen.");
            }

            const encryptedAesKey = base64ToArrayBuffer(encryptedAesKeyBase64);

            // Decrypt raw AES key
            const rawAesKey = await window.crypto.subtle.decrypt(
                { name: "RSA-OAEP" },
                privateKeyObj,
                encryptedAesKey
            );

            // Import AES key
            const aesKey = await window.crypto.subtle.importKey(
                "raw",
                rawAesKey,
                { name: "AES-GCM" },
                false,
                ["decrypt"]
            );

            // Decrypt subject
            const subjectIv = new Uint8Array(base64ToArrayBuffer(payload.subjectIv));
            const subjectCiphertext = base64ToArrayBuffer(payload.subjectCiphertext);
            const decryptedSubjectBytes = await window.crypto.subtle.decrypt(
                { name: "AES-GCM", iv: subjectIv },
                aesKey,
                subjectCiphertext
            );
            const subject = arrayBufferToString(decryptedSubjectBytes);

            // Decrypt body
            const bodyIv = new Uint8Array(base64ToArrayBuffer(payload.bodyIv));
            const bodyCiphertext = base64ToArrayBuffer(payload.bodyCiphertext);
            const decryptedBodyBytes = await window.crypto.subtle.decrypt(
                { name: "AES-GCM", iv: bodyIv },
                aesKey,
                bodyCiphertext
            );
            const body = arrayBufferToString(decryptedBodyBytes);

            return { subject, body };
        },

        // Encrypt file (attachment)
        async encryptFile(file, recipientPublicKeyObj, senderPublicKeyObj) {
            // Generate AES key for file (or reuse message AES key, generating a separate key is cleaner)
            const fileAesKey = await window.crypto.subtle.generateKey(
                { name: "AES-GCM", length: 256 },
                true,
                ["encrypt", "decrypt"]
            );

            const fileBytes = await file.arrayBuffer();
            const iv = window.crypto.getRandomValues(new Uint8Array(12));
            const ciphertext = await window.crypto.subtle.encrypt(
                { name: "AES-GCM", iv: iv },
                fileAesKey,
                fileBytes
            );

            // Export file AES Key
            const rawAesKey = await window.crypto.subtle.exportKey("raw", fileAesKey);

            // Encrypt for recipient & sender
            const encKeyForRecipient = await window.crypto.subtle.encrypt(
                { name: "RSA-OAEP" },
                recipientPublicKeyObj,
                rawAesKey
            );

            const encKeyForSender = await window.crypto.subtle.encrypt(
                { name: "RSA-OAEP" },
                senderPublicKeyObj,
                rawAesKey
            );

            // Encrypt metadata (original filename and content type)
            // Store them in the JSON payload
            const metadataStr = JSON.stringify({
                name: file.name,
                type: file.type || "application/octet-stream"
            });
            const metadataBytes = stringToArrayBuffer(metadataStr);
            const metadataIv = window.crypto.getRandomValues(new Uint8Array(12));
            const metadataCiphertext = await window.crypto.subtle.encrypt(
                { name: "AES-GCM", iv: metadataIv },
                fileAesKey,
                metadataBytes
            );

            const payload = {
                recipientKey: arrayBufferToBase64(encKeyForRecipient),
                senderKey: arrayBufferToBase64(encKeyForSender),
                iv: arrayBufferToBase64(iv),
                metadataIv: arrayBufferToBase64(metadataIv),
                metadataCiphertext: arrayBufferToBase64(metadataCiphertext)
            };

            const encryptedFileBlob = new Blob([ciphertext], { type: "application/octet-stream" });

            return {
                encryptedFileBlob,
                payloadJson: JSON.stringify(payload)
            };
        },

        // Decrypt file (attachment)
        async decryptFile(encryptedArrayBuffer, payloadJsonString, privateKeyObj, isSender) {
            const payload = JSON.parse(payloadJsonString);
            const encryptedAesKeyBase64 = isSender ? payload.senderKey : payload.recipientKey;
            
            const encryptedAesKey = base64ToArrayBuffer(encryptedAesKeyBase64);
            const rawAesKey = await window.crypto.subtle.decrypt(
                { name: "RSA-OAEP" },
                privateKeyObj,
                encryptedAesKey
            );

            const fileAesKey = await window.crypto.subtle.importKey(
                "raw",
                rawAesKey,
                { name: "AES-GCM" },
                false,
                ["decrypt"]
            );

            // Decrypt metadata first
            const metadataIv = new Uint8Array(base64ToArrayBuffer(payload.metadataIv));
            const metadataCipher = base64ToArrayBuffer(payload.metadataCiphertext);
            const decryptedMetadataBytes = await window.crypto.subtle.decrypt(
                { name: "AES-GCM", iv: metadataIv },
                fileAesKey,
                metadataCipher
            );
            const metadata = JSON.parse(arrayBufferToString(decryptedMetadataBytes));

            // Decrypt file bytes
            const fileIv = new Uint8Array(base64ToArrayBuffer(payload.iv));
            const decryptedFileBytes = await window.crypto.subtle.decrypt(
                { name: "AES-GCM", iv: fileIv },
                fileAesKey,
                encryptedArrayBuffer
            );

            return new Blob([decryptedFileBytes], { type: metadata.type });
        },

        // Decrypt metadata only (to get file name and type)
        async decryptFileMetadata(payloadJsonString, privateKeyObj, isSender) {
            try {
                const payload = JSON.parse(payloadJsonString);
                const encryptedAesKeyBase64 = isSender ? payload.senderKey : payload.recipientKey;
                
                const encryptedAesKey = base64ToArrayBuffer(encryptedAesKeyBase64);
                const rawAesKey = await window.crypto.subtle.decrypt(
                    { name: "RSA-OAEP" },
                    privateKeyObj,
                    encryptedAesKey
                );

                const fileAesKey = await window.crypto.subtle.importKey(
                    "raw",
                    rawAesKey,
                    { name: "AES-GCM" },
                    false,
                    ["decrypt"]
                );

                const metadataIv = new Uint8Array(base64ToArrayBuffer(payload.metadataIv));
                const metadataCipher = base64ToArrayBuffer(payload.metadataCiphertext);
                const decryptedMetadataBytes = await window.crypto.subtle.decrypt(
                    { name: "AES-GCM", iv: metadataIv },
                    fileAesKey,
                    metadataCipher
                );
                return JSON.parse(arrayBufferToString(decryptedMetadataBytes));
            } catch (err) {
                console.error("Failed to decrypt file metadata", err);
                return null;
            }
        },

        // Initialize E2E Mailbox (runs automatically on /Messages pages)
        async initializeMailbox() {
            if (!window.location.pathname.toLowerCase().includes('/messages')) {
                return;
            }

            const userEmail = window.userEmail;
            if (!userEmail) return;

            // Check if already unlocked in sessionStorage
            if (sessionStorage.getItem("decryptedPrivateKey")) {
                document.dispatchEvent(new CustomEvent("e2e-ready"));
                return;
            }

            try {
                const statusResponse = await fetch('/Account/GetKeys');
                if (!statusResponse.ok) return;
                const status = await statusResponse.json();

                const storedDerivedKeyBase64 = sessionStorage.getItem("derivedAesKey");
                if (status.hasKeys) {
                    if (storedDerivedKeyBase64) {
                        try {
                            const rawKey = base64ToArrayBuffer(storedDerivedKeyBase64);
                            const aesKey = await window.crypto.subtle.importKey(
                                "raw", rawKey, { name: "AES-GCM" }, false, ["encrypt", "decrypt"]
                            );
                            const privateKeyObj = await E2E.decryptPrivateKey(status.encryptedPrivateKey, aesKey);
                            const privateKeySession = await E2E.exportPrivateKeyToSession(privateKeyObj);
                            sessionStorage.setItem("decryptedPrivateKey", privateKeySession);
                            sessionStorage.setItem("decryptedPrivateKeyEmail", userEmail);
                            sessionStorage.removeItem("derivedAesKey");
                            document.dispatchEvent(new CustomEvent("e2e-ready"));
                        } catch (e) {
                            sessionStorage.removeItem("derivedAesKey");
                            showUnlockModal(status.encryptedPrivateKey, true);
                        }
                    } else {
                        showUnlockModal(status.encryptedPrivateKey, true);
                    }
                } else {
                    if (storedDerivedKeyBase64) {
                        try {
                            const rawKey = base64ToArrayBuffer(storedDerivedKeyBase64);
                            const aesKey = await window.crypto.subtle.importKey(
                                "raw", rawKey, { name: "AES-GCM" }, false, ["encrypt", "decrypt"]
                            );
                            await generateAndSaveKeys(aesKey);
                        } catch (e) {
                            sessionStorage.removeItem("derivedAesKey");
                            showUnlockModal(null, false);
                        }
                    } else {
                        showUnlockModal(null, false);
                    }
                }
            } catch (err) {
                console.error("E2E initialization error", err);
            }
        }
    };

    function showUnlockModal(encryptedPrivateKey, isUnlock) {
        let modal = document.getElementById("e2eUnlockModal");
        if (!modal) {
            // Dynamically inject if not present (should be in layout, but fallback)
            const div = document.createElement("div");
            div.id = "e2eUnlockModal";
            div.className = "e2e-modal-overlay";
            div.innerHTML = `
                <div class="e2e-modal-card">
                    <div class="e2e-modal-header">
                        <i class="fas fa-key e2e-key-icon"></i>
                        <h3 id="e2eModalTitle">Odemčení zabezpečené schránky</h3>
                        <p id="e2eModalDescription">Pro odemčení vašich end-to-end šifrovaných zpráv zadejte své heslo.</p>
                    </div>
                    <div class="e2e-modal-body">
                        <div id="e2eErrorMsg" class="alert alert-danger" style="display: none; padding: 0.5rem; margin-bottom: 1rem; font-size: 0.85rem;"></div>
                        <div class="form-group" style="text-align: left; margin-bottom: 1.25rem;">
                            <label for="e2ePasswordInput" style="display: block; margin-bottom: 0.35rem; font-weight: 600; font-size: 0.85rem;">Heslo k účtu</label>
                            <input type="password" id="e2ePasswordInput" class="form-input" style="width: 100%;" placeholder="Zadejte své heslo..." />
                        </div>
                        <div class="e2e-modal-actions">
                            <button type="button" id="e2eSubmitBtn" class="btn-primary-custom" style="width:100%;">
                                <i class="fas fa-lock-open"></i> Odemknout poštu
                            </button>
                        </div>
                    </div>
                </div>
            `;
            document.body.appendChild(div);
            modal = div;
        }

        const title = document.getElementById("e2eModalTitle");
        const desc = document.getElementById("e2eModalDescription");
        const submitBtn = document.getElementById("e2eSubmitBtn");
        const passwordInput = document.getElementById("e2ePasswordInput");
        const errorMsg = document.getElementById("e2eErrorMsg");

        if (isUnlock) {
            title.textContent = "Odemčení šifrované schránky";
            desc.textContent = "Vaše zprávy jsou šifrovány. Zadejte heslo k účtu pro jejich dešifrování.";
            submitBtn.innerHTML = '<i class="fas fa-lock-open"></i> Odemknout poštu';
        } else {
            title.textContent = "Aktivace zabezpečené schránky";
            desc.textContent = "Zadejte heslo k účtu pro vygenerování vašich end-to-end šifrovacích klíčů.";
            submitBtn.innerHTML = '<i class="fas fa-key"></i> Aktivovat zabezpečení';
        }

        modal.style.display = "flex";
        passwordInput.focus();

        // Listen for Enter key
        passwordInput.onkeydown = function(e) {
            if (e.key === "Enter") {
                submitBtn.click();
            }
        };

        // Handle submission
        submitBtn.onclick = async function() {
            const password = passwordInput.value;
            if (!password) {
                errorMsg.textContent = "Zadejte prosím heslo.";
                errorMsg.style.display = "block";
                return;
            }

            submitBtn.disabled = true;
            submitBtn.innerHTML = '<span class="e2e-loading-spinner"></span> Zpracovávám...';
            errorMsg.style.display = "none";

            try {
                const aesKey = await E2E.deriveKey(password, window.userEmail);
                if (isUnlock) {
                    const privateKeyObj = await E2E.decryptPrivateKey(encryptedPrivateKey, aesKey);
                    const privateKeySession = await E2E.exportPrivateKeyToSession(privateKeyObj);
                    sessionStorage.setItem("decryptedPrivateKey", privateKeySession);
                    sessionStorage.setItem("decryptedPrivateKeyEmail", window.userEmail);
                    modal.style.display = "none";
                    document.dispatchEvent(new CustomEvent("e2e-ready"));
                } else {
                    await generateAndSaveKeys(aesKey);
                    modal.style.display = "none";
                }
            } catch (err) {
                console.error(err);
                errorMsg.textContent = isUnlock ? "Nesprávné heslo nebo chyba dešifrování klíče." : "Chyba při vytváření klíčů.";
                errorMsg.style.display = "block";
                submitBtn.disabled = false;
                submitBtn.innerHTML = isUnlock ? '<i class="fas fa-lock-open"></i> Odemknout poštu' : '<i class="fas fa-key"></i> Aktivovat zabezpečení';
            }
        };
    }

    async function generateAndSaveKeys(aesKey) {
        const keyPair = await E2E.generateKeyPair();
        const publicKeyBase64 = await E2E.exportPublicKey(keyPair.publicKey);
        const encryptedPrivateKeyBase64 = await E2E.encryptPrivateKey(keyPair.privateKey, aesKey);

        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
        const formData = new FormData();
        formData.append("publicKey", publicKeyBase64);
        formData.append("encryptedPrivateKey", encryptedPrivateKeyBase64);
        if (token) {
            formData.append("__RequestVerificationToken", token);
        }

        const saveResponse = await fetch('/Account/SaveKeys', {
            method: 'POST',
            body: formData
        });

        if (!saveResponse.ok) {
            throw new Error("Nepodařilo se uložit klíče na server.");
        }

        const privateKeySession = await E2E.exportPrivateKeyToSession(keyPair.privateKey);
        sessionStorage.setItem("decryptedPrivateKey", privateKeySession);
        sessionStorage.setItem("decryptedPrivateKeyEmail", window.userEmail);
        sessionStorage.removeItem("derivedAesKey");

        document.dispatchEvent(new CustomEvent("e2e-ready"));
    }
})();

// Automatically initialize when page loads
document.addEventListener("DOMContentLoaded", function() {
    // Check if user switched accounts (mismatched cached email)
    const userEmail = window.userEmail;
    if (userEmail) {
        const cachedEmail = sessionStorage.getItem("decryptedPrivateKeyEmail");
        if (cachedEmail && cachedEmail.toLowerCase().trim() !== userEmail.toLowerCase().trim()) {
            sessionStorage.removeItem("decryptedPrivateKey");
            sessionStorage.removeItem("decryptedPrivateKeyEmail");
            sessionStorage.removeItem("derivedAesKey");
        }
    }
    E2E.initializeMailbox();
});
