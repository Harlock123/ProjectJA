// SPDX-License-Identifier: BUSL-1.1
// Browser-direct upload: fetch presigned URL from the server, PUT the bytes
// straight to S3 (or R2/MinIO), then tell the server the upload completed.
//
// Wired up from IssueDetail.razor via JSRuntime.InvokeVoidAsync("projectja.uploads.init", ...).

(function () {
    window.projectja = window.projectja || {};
    window.projectja.uploads = {
        init: function (dotnetRef, inputId, issueId) {
            const input = document.getElementById(inputId);
            if (!input || input.dataset.bound === '1') return;
            input.dataset.bound = '1';

            input.addEventListener('change', async () => {
                const files = Array.from(input.files || []);
                input.value = '';
                for (const file of files) {
                    try {
                        const contentType = file.type || 'application/octet-stream';

                        const beginResp = await fetch(`/api/issues/${issueId}/attachments/begin`, {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/json' },
                            credentials: 'same-origin',
                            body: JSON.stringify({
                                fileName: file.name,
                                contentType: contentType,
                                sizeBytes: file.size,
                            }),
                        });
                        if (!beginResp.ok) throw new Error(`begin failed: HTTP ${beginResp.status}`);
                        const begin = await beginResp.json();

                        const putResp = await fetch(begin.uploadUrl, {
                            method: 'PUT',
                            body: file,
                            headers: { 'Content-Type': contentType },
                        });
                        if (!putResp.ok) throw new Error(`PUT failed: HTTP ${putResp.status}`);

                        const completeResp = await fetch(
                            `/api/issues/${issueId}/attachments/${begin.attachmentId}/complete`,
                            {
                                method: 'POST',
                                headers: { 'Content-Type': 'application/json' },
                                credentials: 'same-origin',
                                body: JSON.stringify({
                                    storageKey: begin.storageKey,
                                    fileName: begin.fileName,
                                    contentType: begin.contentType,
                                    sizeBytes: begin.sizeBytes,
                                }),
                            }
                        );
                        if (!completeResp.ok) throw new Error(`complete failed: HTTP ${completeResp.status}`);

                        await dotnetRef.invokeMethodAsync('OnUploadComplete', begin.attachmentId);
                    } catch (e) {
                        await dotnetRef.invokeMethodAsync('OnUploadError', e.message || String(e));
                    }
                }
            });
        }
    };
})();
