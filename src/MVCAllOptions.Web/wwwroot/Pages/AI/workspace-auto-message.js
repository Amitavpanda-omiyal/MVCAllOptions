/**
 * workspace-auto-message.js
 *
 * When a user navigates to an AI Management workspace chat page
 * (e.g. /AIManagement/Workspaces/OpenAIRAGWorkspace), this script
 * automatically asks about recently verified books by populating the
 * chat input and clicking the Send button.
 */
(function () {
    'use strict';

    // Only run on AI Management workspace pages
    if (typeof window === 'undefined' ||
        !window.location.pathname.toLowerCase().startsWith('/aimanagement/workspaces/')) {
        return;
    }

    var MAX_ATTEMPTS = 40;   // 40 × 250 ms = 10 seconds max wait
    var INTERVAL_MS  = 250;
    var attempts     = 0;
    var sent         = false;

    var AUTO_MESSAGE =
        'Tell me about the verification status of recently added books. ' +
        'Which books exist in the real world and which ones were not found?';

    function tryAutoSend() {
        if (sent) return;

        attempts++;
        if (attempts > MAX_ATTEMPTS) return;

        // Find the textarea and Send button in the chat widget
        var input   = document.querySelector('textarea[placeholder], input[placeholder*="message"], textarea');
        var sendBtn = document.querySelector('button[class*="send"], button:not([class*="new"]):not([class*="close"]):not([class*="rename"]):not([class*="delete"])');

        // Look for the Send button more specifically
        if (!sendBtn) {
            var buttons = document.querySelectorAll('button');
            for (var i = 0; i < buttons.length; i++) {
                if (buttons[i].textContent.trim() === 'Send') {
                    sendBtn = buttons[i];
                    break;
                }
            }
        }

        if (!input || !sendBtn) {
            setTimeout(tryAutoSend, INTERVAL_MS);
            return;
        }

        // Use the native input setter to trigger Vue/React change events
        var nativeInputValueSetter = Object.getOwnPropertyDescriptor(
            window.HTMLTextAreaElement.prototype, 'value'
        ) || Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value');

        if (nativeInputValueSetter && nativeInputValueSetter.set) {
            nativeInputValueSetter.set.call(input, AUTO_MESSAGE);
        } else {
            input.value = AUTO_MESSAGE;
        }

        // Dispatch input event so the framework picks up the new value
        input.dispatchEvent(new Event('input', { bubbles: true }));
        input.dispatchEvent(new Event('change', { bubbles: true }));

        sent = true;

        // Re-query Send button at click time to avoid stale DOM references
        setTimeout(function () {
            var btn = document.querySelector('button:not([style*="display:none"])' );
            var allBtns = document.querySelectorAll('button');
            for (var j = 0; j < allBtns.length; j++) {
                if (allBtns[j].textContent.trim() === 'Send' &&
                    !allBtns[j].disabled) {
                    allBtns[j].click();
                    return;
                }
            }
        }, 150);
    }

    // Give the page 1.5 s to render the widget before polling
    setTimeout(tryAutoSend, 1500);
})();
