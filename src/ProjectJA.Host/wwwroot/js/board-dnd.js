// SPDX-License-Identifier: BUSL-1.1
// HTML5 drag-drop for the board. Sets dataTransfer in dragstart (required by
// Safari/Firefox for drag to even begin) and posts back to .NET on drop.
//
// Wired up from Board.razor via:
//   JSRuntime.InvokeVoidAsync("projectja.board.init", dotnetRef);
// The Razor page must render .board-column elements with data-state-id="<guid>"
// (the workflow state's id — boards are now driven by the project's workflow,
// not a fixed enum) and .board-card elements with data-issue-id="<guid>".

(function () {
    window.projectja = window.projectja || {};
    window.projectja.board = {
        init: function (dotnetRef) {
            // Cards: enable dragstart with proper dataTransfer.
            document.querySelectorAll('.board-card[data-issue-id]').forEach(card => {
                if (card.dataset.dndBound === '1') return;
                card.dataset.dndBound = '1';

                card.addEventListener('dragstart', (e) => {
                    const id = card.getAttribute('data-issue-id');
                    if (!id) return;
                    e.dataTransfer.setData('text/plain', id);
                    e.dataTransfer.effectAllowed = 'move';
                    card.classList.add('board-card-dragging');
                });
                card.addEventListener('dragend', () => {
                    card.classList.remove('board-card-dragging');
                });
            });

            // Columns: allow drop + dispatch to .NET.
            document.querySelectorAll('.board-column[data-state-id]').forEach(col => {
                if (col.dataset.dndBound === '1') return;
                col.dataset.dndBound = '1';

                col.addEventListener('dragover', (e) => {
                    e.preventDefault();
                    e.dataTransfer.dropEffect = 'move';
                    col.classList.add('board-column-hover');
                });
                col.addEventListener('dragleave', () => {
                    col.classList.remove('board-column-hover');
                });
                col.addEventListener('drop', async (e) => {
                    e.preventDefault();
                    col.classList.remove('board-column-hover');
                    const issueId = e.dataTransfer.getData('text/plain');
                    const stateId = col.getAttribute('data-state-id');
                    if (!issueId || !stateId) return;
                    try {
                        await dotnetRef.invokeMethodAsync('OnCardDropped', issueId, stateId);
                    } catch (err) {
                        console.error('Board drop callback failed:', err);
                    }
                });
            });
        }
    };
})();
