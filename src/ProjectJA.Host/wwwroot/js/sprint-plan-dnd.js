// SPDX-License-Identifier: BUSL-1.1
// HTML5 drag-drop for the sprint planning page. Two drop targets:
//   .plan-column[data-target="backlog"]   → assign null
//   .plan-column[data-target="sprint"]    → assign the currently-selected sprint
// Cards have .plan-card[data-issue-id]. Mirrors board-dnd.js (Safari/Firefox
// need dataTransfer.setData in dragstart). On drop, invokes:
//   dotnetRef.OnIssueDropped(issueId, target)
// where target is "backlog" | "sprint".

(function () {
    window.projectja = window.projectja || {};
    window.projectja.sprintPlan = {
        init: function (dotnetRef) {
            document.querySelectorAll('.plan-card[data-issue-id]').forEach(card => {
                if (card.dataset.dndBound === '1') return;
                card.dataset.dndBound = '1';

                card.addEventListener('dragstart', (e) => {
                    const id = card.getAttribute('data-issue-id');
                    if (!id) return;
                    e.dataTransfer.setData('text/plain', id);
                    e.dataTransfer.effectAllowed = 'move';
                    card.classList.add('plan-card-dragging');
                });
                card.addEventListener('dragend', () => {
                    card.classList.remove('plan-card-dragging');
                });
            });

            document.querySelectorAll('.plan-column[data-target]').forEach(col => {
                if (col.dataset.dndBound === '1') return;
                col.dataset.dndBound = '1';

                col.addEventListener('dragover', (e) => {
                    e.preventDefault();
                    e.dataTransfer.dropEffect = 'move';
                    col.classList.add('plan-column-hover');
                });
                col.addEventListener('dragleave', () => {
                    col.classList.remove('plan-column-hover');
                });
                col.addEventListener('drop', async (e) => {
                    e.preventDefault();
                    col.classList.remove('plan-column-hover');
                    const id = e.dataTransfer.getData('text/plain');
                    const target = col.getAttribute('data-target');
                    if (!id || !target) return;
                    try {
                        await dotnetRef.invokeMethodAsync('OnIssueDropped', id, target);
                    } catch (err) {
                        console.error('Plan drop callback failed:', err);
                    }
                });
            });
        }
    };
})();
