/**
 * ChatBox AI - Modern Client Logic
 * Handles Ollama models, multi-file uploads (PDF, Word, TXT), chat history, and Markdown rendering.
 */
document.addEventListener('DOMContentLoaded', () => {
    // State variables
    let currentConversationId = null;
    let stagedFiles = [];
    let isGenerating = false;
    let conversations = [];

    // DOM Elements
    const chatMessages = document.getElementById('chatMessages');
    const welcomeScreen = document.getElementById('welcomeScreen');
    const promptInput = document.getElementById('promptInput');
    const chatForm = document.getElementById('chatForm');
    const sendBtn = document.getElementById('sendBtn');
    const sendIcon = document.getElementById('sendIcon');
    const fileUploadInput = document.getElementById('fileUploadInput');
    const attachFileBtn = document.getElementById('attachFileBtn');
    const attachedFilesBar = document.getElementById('attachedFilesBar');
    const attachedFilesList = document.getElementById('attachedFilesList');
    const modelSelect = document.getElementById('modelSelect');
    const refreshModelsBtn = document.getElementById('refreshModelsBtn');
    const refreshModelsIcon = document.getElementById('refreshModelsIcon');
    const ollamaStatusBadge = document.getElementById('ollamaStatusBadge');
    const ollamaStatusText = document.getElementById('ollamaStatusText');
    const customModelBtn = document.getElementById('customModelBtn');
    const customModelRow = document.getElementById('customModelRow');
    const manualModelInput = document.getElementById('manualModelInput');
    const applyManualModelBtn = document.getElementById('applyManualModelBtn');
    const closeManualModelBtn = document.getElementById('closeManualModelBtn');
    const conversationsList = document.getElementById('conversationsList');
    const newChatBtn = document.getElementById('newChatBtn');
    const refreshConversationsBtn = document.getElementById('refreshConversationsBtn');
    const searchChatsInput = document.getElementById('searchChatsInput');
    const currentChatTitle = document.getElementById('currentChatTitle');
    const themeToggleBtn = document.getElementById('themeToggleBtn');
    const themeIcon = document.getElementById('themeIcon');
    const sidebar = document.getElementById('sidebar');
    const toggleSidebarBtn = document.getElementById('toggleSidebarBtn');
    const closeSidebarBtn = document.getElementById('closeSidebarBtn');

    // 1. Theme Management (Dark / Light)
    initTheme();

    function initTheme() {
        const savedTheme = localStorage.getItem('chatbox_theme') || 'dark';
        document.documentElement.setAttribute('data-bs-theme', savedTheme);
        updateThemeIcon(savedTheme);

        themeToggleBtn.addEventListener('click', () => {
            const currentTheme = document.documentElement.getAttribute('data-bs-theme');
            const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
            document.documentElement.setAttribute('data-bs-theme', newTheme);
            localStorage.setItem('chatbox_theme', newTheme);
            updateThemeIcon(newTheme);
        });
    }

    function updateThemeIcon(theme) {
        if (theme === 'dark') {
            themeIcon.className = 'bi bi-sun-fill text-warning';
        } else {
            themeIcon.className = 'bi bi-moon-stars-fill text-primary';
        }
    }

    // 2. Sidebar Mobile Toggle
    if (toggleSidebarBtn) {
        toggleSidebarBtn.addEventListener('click', () => sidebar.classList.add('open'));
    }
    if (closeSidebarBtn) {
        closeSidebarBtn.addEventListener('click', () => sidebar.classList.remove('open'));
    }

    // 3. Load Ollama Models (STRICT NO-RETRY on failure)
    loadOllamaModels();

    async function loadOllamaModels() {
        refreshModelsIcon.classList.add('spin-animation');
        ollamaStatusText.textContent = 'در حال بررسی اولاما...';
        ollamaStatusBadge.className = 'badge bg-secondary-subtle text-secondary py-2 px-2';

        try {
            const res = await fetch('/api/models');
            const data = await res.json();

            modelSelect.innerHTML = '';

            if (data.isAvailable && data.models && data.models.length > 0) {
                // Models available on user's Ollama
                ollamaStatusBadge.className = 'badge bg-success-subtle text-success border border-success-subtle py-2 px-2';
                ollamaStatusBadge.querySelector('.status-dot').className = 'status-dot status-online';
                ollamaStatusText.textContent = `متصل (${data.models.length} مدل)`;

                let hasQwen = false;
                data.models.forEach(m => {
                    const opt = document.createElement('option');
                    opt.value = m.name;
                    opt.textContent = `${m.displayName} (${m.sizeFormatted})`;
                    if (m.name.toLowerCase().includes('qwen3.8')) {
                        opt.selected = true;
                        hasQwen = true;
                    }
                    modelSelect.appendChild(opt);
                });

                if (!hasQwen && data.models.length > 0) {
                    modelSelect.selectedIndex = 0;
                }
            } else {
                // Ollama offline or no models found
                // STRICT RULE: No retry!
                ollamaStatusBadge.className = 'badge bg-warning-subtle text-warning border border-warning-subtle py-2 px-2';
                ollamaStatusBadge.querySelector('.status-dot').className = 'status-dot status-warning';
                ollamaStatusText.textContent = 'مدلی یافت نشد (بدون Retry)';

                const optDefault = document.createElement('option');
                optDefault.value = 'qwen3.8';
                optDefault.textContent = 'qwen3.8 (ورودی پیش‌فرض)';
                optDefault.selected = true;
                modelSelect.appendChild(optDefault);
            }
        } catch (err) {
            // Error connecting to backend/ollama - NO RETRY
            ollamaStatusBadge.className = 'badge bg-danger-subtle text-danger border border-danger-subtle py-2 px-2';
            ollamaStatusBadge.querySelector('.status-dot').className = 'status-dot status-offline';
            ollamaStatusText.textContent = 'عدم دسترسی به اولاما';

            modelSelect.innerHTML = '<option value="qwen3.8">qwen3.8 (پیش‌فرض)</option>';
        } finally {
            refreshModelsIcon.classList.remove('spin-animation');
        }
    }

    refreshModelsBtn.addEventListener('click', loadOllamaModels);

    // Manual Model Input Row
    customModelBtn.addEventListener('click', () => {
        customModelRow.classList.toggle('d-none');
        if (!customModelRow.classList.contains('d-none')) {
            manualModelInput.focus();
        }
    });

    closeManualModelBtn.addEventListener('click', () => {
        customModelRow.classList.add('d-none');
    });

    applyManualModelBtn.addEventListener('click', () => {
        const customName = manualModelInput.value.trim();
        if (customName) {
            let opt = Array.from(modelSelect.options).find(o => o.value === customName);
            if (!opt) {
                opt = document.createElement('option');
                opt.value = customName;
                opt.textContent = `${customName} (سفارشی)`;
                modelSelect.appendChild(opt);
            }
            opt.selected = true;
            customModelRow.classList.add('d-none');
        }
    });

    // 4. File Upload & Drag & Drop Handling
    attachFileBtn.addEventListener('click', () => fileUploadInput.click());

    fileUploadInput.addEventListener('change', (e) => {
        if (e.target.files) {
            addFiles(Array.from(e.target.files));
        }
        fileUploadInput.value = '';
    });

    function addFiles(files) {
        files.forEach(f => {
            // Avoid duplicate by name and size
            if (!stagedFiles.some(existing => existing.name === f.name && existing.size === f.size)) {
                stagedFiles.push(f);
            }
        });
        renderAttachedChips();
    }

    function removeFile(index) {
        stagedFiles.splice(index, 1);
        renderAttachedChips();
    }

    function renderAttachedChips() {
        if (stagedFiles.length === 0) {
            attachedFilesBar.classList.add('d-none');
            attachedFilesList.innerHTML = '';
            return;
        }

        attachedFilesBar.classList.remove('d-none');
        attachedFilesList.innerHTML = '';

        stagedFiles.forEach((file, index) => {
            const chip = document.createElement('div');
            chip.className = 'attached-chip';

            const ext = getFileExtension(file.name);
            const iconClass = getFileIconClass(ext);

            chip.innerHTML = `
                <i class="${iconClass}"></i>
                <span>${escapeHtml(file.name)}</span>
                <small class="text-muted">(${formatBytes(file.size)})</small>
                <button type="button" class="btn-remove-chip ms-1" data-index="${index}" title="حذف فایل">
                    <i class="bi bi-x"></i>
                </button>
            `;

            chip.querySelector('.btn-remove-chip').addEventListener('click', () => removeFile(index));
            attachedFilesList.appendChild(chip);
        });
    }

    // Drag and drop onto chat main
    const chatMain = document.querySelector('.chat-main');
    ['dragenter', 'dragover'].forEach(eventName => {
        chatMain.addEventListener(eventName, (e) => {
            e.preventDefault();
            chatMain.classList.add('drag-active');
        }, false);
    });

    ['dragleave', 'drop'].forEach(eventName => {
        chatMain.addEventListener(eventName, (e) => {
            e.preventDefault();
            chatMain.classList.remove('drag-active');
        }, false);
    });

    chatMain.addEventListener('drop', (e) => {
        if (e.dataTransfer && e.dataTransfer.files) {
            addFiles(Array.from(e.dataTransfer.files));
        }
    });

    // 5. Textarea Auto-expand & Keyboard Enter
    promptInput.addEventListener('input', () => {
        promptInput.style.height = 'auto';
        promptInput.style.height = Math.min(promptInput.scrollHeight, 180) + 'px';
    });

    promptInput.addEventListener('keydown', (e) => {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            submitChat();
        }
    });

    chatForm.addEventListener('submit', (e) => {
        e.preventDefault();
        submitChat();
    });

    // Example prompt cards click
    document.querySelectorAll('.feature-prompt-card').forEach(card => {
        card.addEventListener('click', () => {
            const prompt = card.getAttribute('data-prompt');
            if (prompt) {
                promptInput.value = prompt;
                promptInput.focus();
                promptInput.dispatchEvent(new Event('input'));
            }
        });
    });

    // 6. Submit Prompt and Files
    async function submitChat() {
        const promptText = promptInput.value.trim();
        if ((!promptText && stagedFiles.length === 0) || isGenerating) {
            return;
        }

        const selectedModel = modelSelect.value || 'qwen3.8';
        const filesToSend = [...stagedFiles];

        // Clear input and files immediately
        promptInput.value = '';
        promptInput.style.height = 'auto';
        stagedFiles = [];
        renderAttachedChips();

        // Hide welcome screen
        if (welcomeScreen) {
            welcomeScreen.style.display = 'none';
        }

        // Render User Message in UI
        appendUserMessage(promptText, filesToSend);

        // Render Thinking / Loading indicator
        const thinkingRow = appendThinkingIndicator(selectedModel);
        chatMessages.scrollTop = chatMessages.scrollHeight;

        setGeneratingState(true);

        // Prepare FormData
        const formData = new FormData();
        formData.append('Prompt', promptText);
        formData.append('Model', selectedModel);
        if (currentConversationId) {
            formData.append('ConversationId', currentConversationId);
        }
        filesToSend.forEach(f => {
            formData.append('Files', f);
        });

        try {
            const res = await fetch('/api/chat/send', {
                method: 'POST',
                body: formData
            });

            thinkingRow.remove();

            if (!res.ok) {
                const errData = await res.json().catch(() => ({}));
                appendAssistantMessage(`⚠️ **خطا:** ${errData.message || 'خطای غیرمنتظره در سرور رخ داد.'}`, selectedModel, false);
                return;
            }

            const data = await res.json();
            currentConversationId = data.conversationId;
            currentChatTitle.textContent = data.conversationTitle;

            appendAssistantMessage(data.content, data.modelUsed, data.success);

            // Refresh conversations list in sidebar
            loadConversations();

        } catch (err) {
            thinkingRow.remove();
            appendAssistantMessage(`⚠️ **خطای شبکه:** امکان ارتباط با سرور برقرار نشد. (${err.message})`, selectedModel, false);
        } finally {
            setGeneratingState(false);
            chatMessages.scrollTop = chatMessages.scrollHeight;
        }
    }

    function setGeneratingState(generating) {
        isGenerating = generating;
        sendBtn.disabled = generating;
        if (generating) {
            sendIcon.className = 'spinner-border spinner-border-sm';
        } else {
            sendIcon.className = 'bi bi-arrow-up-circle-fill fs-4';
            promptInput.focus();
        }
    }

    // 7. Render Messages
    function appendUserMessage(text, files) {
        const row = document.createElement('div');
        row.className = 'message-row user';

        let filesHtml = '';
        if (files && files.length > 0) {
            filesHtml = '<div class="message-attachments">';
            files.forEach(f => {
                const ext = getFileExtension(f.name);
                const iconClass = getFileIconClass(ext);
                filesHtml += `
                    <div class="file-badge">
                        <i class="${iconClass}"></i>
                        <span class="file-name">${escapeHtml(f.name)}</span>
                        <small class="text-muted">(${formatBytes(f.size)})</small>
                    </div>
                `;
            });
            filesHtml += '</div>';
        }

        row.innerHTML = `
            <div class="message-avatar">
                <i class="bi bi-person-fill"></i>
            </div>
            <div class="message-body">
                <div class="message-bubble">
                    ${filesHtml}
                    <div>${escapeHtml(text).replace(/\n/g, '<br/>')}</div>
                </div>
                <div class="message-meta justify-content-end">
                    <span>${getCurrentPersianTime()}</span>
                </div>
            </div>
        `;

        chatMessages.appendChild(row);
    }

    function appendAssistantMessage(content, modelUsed, success = true) {
        const row = document.createElement('div');
        row.className = 'message-row assistant';

        // Render Markdown safely
        const renderedHtml = marked.parse(content);

        row.innerHTML = `
            <div class="message-avatar">
                <i class="bi bi-stars"></i>
            </div>
            <div class="message-body">
                <div class="message-bubble">
                    ${renderedHtml}
                </div>
                <div class="message-meta">
                    <span class="badge bg-body-secondary text-secondary">${escapeHtml(modelUsed || 'Ollama')}</span>
                    <span>${getCurrentPersianTime()}</span>
                    <button class="btn btn-sm btn-link text-muted p-0 ms-2 copy-btn" title="کپی متن">
                        <i class="bi bi-clipboard"></i> کپی
                    </button>
                </div>
            </div>
        `;

        // Highlight code blocks
        row.querySelectorAll('pre code').forEach((block) => {
            hljs.highlightElement(block);
        });

        // Copy button handler
        const copyBtn = row.querySelector('.copy-btn');
        copyBtn.addEventListener('click', () => {
            navigator.clipboard.writeText(content).then(() => {
                copyBtn.innerHTML = '<i class="bi bi-check2 text-success"></i> کپی شد!';
                setTimeout(() => {
                    copyBtn.innerHTML = '<i class="bi bi-clipboard"></i> کپی';
                }, 2000);
            });
        });

        chatMessages.appendChild(row);
    }

    function appendThinkingIndicator(model) {
        const row = document.createElement('div');
        row.className = 'message-row assistant';
        row.innerHTML = `
            <div class="message-avatar">
                <i class="bi bi-stars"></i>
            </div>
            <div class="message-body">
                <div class="message-bubble">
                    <div class="d-flex align-items-center gap-3">
                        <div class="typing-indicator">
                            <span class="typing-dot"></span>
                            <span class="typing-dot"></span>
                            <span class="typing-dot"></span>
                        </div>
                        <span class="small text-muted">مدل <strong>${escapeHtml(model)}</strong> در حال پردازش و استخراج اسناد است... (بدون Retry)</span>
                    </div>
                </div>
            </div>
        `;
        chatMessages.appendChild(row);
        return row;
    }

    // 8. Conversations History Management (SSMS + EF Core)
    loadConversations();

    async function loadConversations() {
        try {
            const res = await fetch('/api/conversations');
            if (!res.ok) return;

            conversations = await res.json();
            renderConversationsList(conversations);
        } catch (err) {
            conversationsList.innerHTML = '<div class="text-center p-3 text-danger small">خطا در دریافت تاریخچه</div>';
        }
    }

    refreshConversationsBtn.addEventListener('click', loadConversations);

    function renderConversationsList(list) {
        if (!list || list.length === 0) {
            conversationsList.innerHTML = '<div class="text-center p-4 text-muted small">هنوز گفتگویی ذخیره نشده است.</div>';
            return;
        }

        conversationsList.innerHTML = '';
        list.forEach(conv => {
            const item = document.createElement('div');
            item.className = `conversation-item ${conv.id === currentConversationId ? 'active' : ''}`;
            item.setAttribute('data-id', conv.id);

            item.innerHTML = `
                <div class="d-flex align-items-center gap-2 overflow-hidden">
                    <i class="bi bi-chat-left-text text-muted"></i>
                    <div>
                        <div class="item-title" title="${escapeHtml(conv.title)}">${escapeHtml(conv.title)}</div>
                        <div class="item-meta">
                            <span>${conv.modelName || 'qwen3.8'}</span>
                            <span>•</span>
                            <span>${conv.messageCount} پیام</span>
                        </div>
                    </div>
                </div>
                <button class="delete-conv-btn" title="حذف گفتگو">
                    <i class="bi bi-trash3"></i>
                </button>
            `;

            // Click to load conversation
            item.addEventListener('click', (e) => {
                if (e.target.closest('.delete-conv-btn')) return;
                loadConversationDetail(conv.id);
            });

            // Delete conversation
            const delBtn = item.querySelector('.delete-conv-btn');
            delBtn.addEventListener('click', (e) => {
                e.stopPropagation();
                if (confirm('آیا از حذف این گفتگو اطمینان دارید؟')) {
                    deleteConversation(conv.id);
                }
            });

            conversationsList.appendChild(item);
        });
    }

    // Search filter for conversations
    searchChatsInput.addEventListener('input', (e) => {
        const query = e.target.value.toLowerCase().trim();
        if (!query) {
            renderConversationsList(conversations);
            return;
        }
        const filtered = conversations.filter(c => 
            c.title.toLowerCase().includes(query) || 
            (c.modelName && c.modelName.toLowerCase().includes(query))
        );
        renderConversationsList(filtered);
    });

    // Load specific conversation from SSMS
    async function loadConversationDetail(id) {
        try {
            const res = await fetch(`/api/conversations/${id}`);
            if (!res.ok) return;

            const detail = await res.json();
            currentConversationId = detail.id;
            currentChatTitle.textContent = detail.title;

            // Highlight active in sidebar
            document.querySelectorAll('.conversation-item').forEach(el => {
                el.classList.toggle('active', parseInt(el.getAttribute('data-id')) === id);
            });

            // Hide welcome screen
            if (welcomeScreen) {
                welcomeScreen.style.display = 'none';
            }

            // Clear messages and render saved ones
            chatMessages.innerHTML = '';
            detail.messages.forEach(msg => {
                if (msg.role === 'user') {
                    appendSavedUserMessage(msg);
                } else if (msg.role === 'assistant') {
                    appendAssistantMessage(msg.content, detail.modelName);
                }
            });

            chatMessages.scrollTop = chatMessages.scrollHeight;

            // Close sidebar on mobile
            sidebar.classList.remove('open');

        } catch (err) {
            alert('خطا در بارگذاری پیام‌های گفتگو: ' + err.message);
        }
    }

    function appendSavedUserMessage(msg) {
        const row = document.createElement('div');
        row.className = 'message-row user';

        let filesHtml = '';
        if (msg.attachments && msg.attachments.length > 0) {
            filesHtml = '<div class="message-attachments">';
            msg.attachments.forEach(a => {
                const iconClass = getFileIconClass(a.fileExtension);
                filesHtml += `
                    <div class="file-badge">
                        <i class="${iconClass}"></i>
                        <span class="file-name">${escapeHtml(a.fileName)}</span>
                        <small class="text-muted">(${escapeHtml(a.fileSizeFormatted)})</small>
                    </div>
                `;
            });
            filesHtml += '</div>';
        }

        row.innerHTML = `
            <div class="message-avatar">
                <i class="bi bi-person-fill"></i>
            </div>
            <div class="message-body">
                <div class="message-bubble">
                    ${filesHtml}
                    <div>${escapeHtml(msg.content).replace(/\n/g, '<br/>')}</div>
                </div>
                <div class="message-meta justify-content-end">
                    <span>${formatDateToPersian(msg.createdAt)}</span>
                </div>
            </div>
        `;

        chatMessages.appendChild(row);
    }

    async function deleteConversation(id) {
        try {
            const res = await fetch(`/api/conversations/${id}`, { method: 'DELETE' });
            if (res.ok) {
                if (currentConversationId === id) {
                    resetToNewChat();
                }
                loadConversations();
            }
        } catch (err) {
            alert('خطا در حذف گفتگو: ' + err.message);
        }
    }

    // New Chat Button
    newChatBtn.addEventListener('click', resetToNewChat);

    function resetToNewChat() {
        currentConversationId = null;
        currentChatTitle.textContent = 'گفتگوی جدید';
        chatMessages.innerHTML = '';
        if (welcomeScreen) {
            chatMessages.appendChild(welcomeScreen);
            welcomeScreen.style.display = 'block';
        }
        stagedFiles = [];
        renderAttachedChips();
        promptInput.value = '';
        promptInput.focus();

        document.querySelectorAll('.conversation-item').forEach(el => el.classList.remove('active'));
        sidebar.classList.remove('open');
    }

    // Helpers
    function getFileExtension(name) {
        if (!name) return '';
        const idx = name.lastIndexOf('.');
        return idx !== -1 ? name.substring(idx).toLowerCase() : '';
    }

    function getFileIconClass(ext) {
        switch (ext) {
            case '.pdf': return 'bi bi-file-earmark-pdf-fill file-pdf';
            case '.docx':
            case '.doc': return 'bi bi-file-earmark-word-fill file-word';
            case '.txt':
            case '.text':
            case '.md': return 'bi bi-file-earmark-text-fill file-text';
            case '.json':
            case '.csv':
            case '.xml': return 'bi bi-file-earmark-code-fill text-warning';
            default: return 'bi bi-file-earmark-fill text-secondary';
        }
    }

    function formatBytes(bytes) {
        if (!bytes || bytes <= 0) return '0 B';
        const k = 1024;
        const sizes = ['B', 'KB', 'MB', 'GB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
    }

    function escapeHtml(str) {
        if (!str) return '';
        const div = document.createElement('div');
        div.textContent = str;
        return div.innerHTML;
    }

    function getCurrentPersianTime() {
        const now = new Date();
        return now.toLocaleTimeString('fa-IR', { hour: '2-digit', minute: '2-digit' });
    }

    function formatDateToPersian(dateStr) {
        if (!dateStr) return '';
        const date = new Date(dateStr);
        return date.toLocaleDateString('fa-IR', { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
    }
});
