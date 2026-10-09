// ==========================================================================
// THE GREAT AYURVEDA — CLIENT INTERACTION ENGINE
// Features: Smart Search, Prakriti Quiz, Deals Timer, Carousel, Ajax Cart
// ==========================================================================

document.addEventListener("DOMContentLoaded", function () {
    initSearchBox();
    initConfirmModal();
    initIdleTimeoutModal();
    initAjaxCart();
    initPrakritiQuiz();
    initCountdownTimer();
    initTouchCarousel();
    initDinacharyaTabs();
    initScrollReveal();
    initCouponCopy();
});

// --------------------------------------------------------------------------
// 1. SMART SEARCH WITH 150MS DEBOUNCE & KEYBOARD NAV
// --------------------------------------------------------------------------
// 1. SMART SEARCH WITH 150MS DEBOUNCE, ABORTCONTROLLER, HIGHLIGHTS & KEYBOARD NAV
// --------------------------------------------------------------------------
function initSearchBox() {
    const searchInputs = document.querySelectorAll(".tga-search-input");

    searchInputs.forEach(input => {
        const container = input.closest(".search-container");
        if (!container) return;

        const dropdown = container.querySelector(".search-results-dropdown");
        const clearBtn = container.querySelector(".btn-clear-search");
        const form = container.querySelector("form");
        if (!dropdown) return;

        const scope = input.getAttribute("data-scope") || "public";
        let debounceTimer = null;
        let abortController = null;
        let selectedIndex = -1;

        // Toggle Clear Button
        function toggleClearBtn() {
            if (clearBtn) {
                if (input.value.trim().length > 0) {
                    clearBtn.classList.remove("d-none");
                } else {
                    clearBtn.classList.add("d-none");
                }
            }
        }

        // On Clear Button Click
        if (clearBtn) {
            clearBtn.addEventListener("click", function () {
                input.value = "";
                toggleClearBtn();
                renderRecentAndTrending(dropdown, scope, input);
                input.focus();
            });
        }

        // Focus event: show recent and trending if empty
        input.addEventListener("focus", function () {
            toggleClearBtn();
            const query = input.value.trim();
            if (query.length < 2) {
                renderRecentAndTrending(dropdown, scope, input);
            }
        });

        // Input event: 150ms debounce + AbortController cancellation
        input.addEventListener("input", function () {
            toggleClearBtn();
            clearTimeout(debounceTimer);
            const query = input.value.trim();

            if (query.length < 2) {
                renderRecentAndTrending(dropdown, scope, input);
                return;
            }

            debounceTimer = setTimeout(() => {
                if (abortController) {
                    abortController.abort();
                }
                abortController = new AbortController();

                dropdown.innerHTML = `<div class="p-3 text-muted small text-center"><span class="spinner-border spinner-border-sm me-2 text-success"></span>Searching classical formulations...</div>`;
                dropdown.classList.add("show");

                fetch(`/search/suggest?q=${encodeURIComponent(query)}&scope=${encodeURIComponent(scope)}`, {
                    signal: abortController.signal
                })
                .then(res => {
                    if (!res.ok) throw new Error("Search network error");
                    return res.json();
                })
                .then(data => {
                    selectedIndex = -1;
                    renderSuggestions(data, dropdown, query, scope, input);
                })
                .catch(err => {
                    if (err.name !== "AbortError") {
                        console.error("Search suggest error:", err);
                    }
                });
            }, 150);
        });

        // Form Submit: Save recent search
        if (form) {
            form.addEventListener("submit", function () {
                const q = input.value.trim();
                if (q) saveRecentSearch(scope, q);
            });
        }

        // Keyboard navigation (ArrowDown, ArrowUp, Enter, Escape)
        input.addEventListener("keydown", function (e) {
            const items = dropdown.querySelectorAll(".search-item");

            if (e.key === "ArrowDown") {
                if (!items.length) return;
                e.preventDefault();
                selectedIndex = (selectedIndex + 1) % items.length;
                updateActiveItem(items, selectedIndex);
            } else if (e.key === "ArrowUp") {
                if (!items.length) return;
                e.preventDefault();
                selectedIndex = (selectedIndex - 1 + items.length) % items.length;
                updateActiveItem(items, selectedIndex);
            } else if (e.key === "Enter") {
                if (selectedIndex >= 0 && items[selectedIndex]) {
                    e.preventDefault();
                    items[selectedIndex].click();
                } else {
                    const q = input.value.trim();
                    if (q) saveRecentSearch(scope, q);
                }
            } else if (e.key === "Escape") {
                dropdown.classList.remove("show");
            }
        });

        // Click outside closes dropdown
        document.addEventListener("click", function (e) {
            if (!container.contains(e.target)) {
                dropdown.classList.remove("show");
            }
        });
    });
}

function updateActiveItem(items, index) {
    items.forEach((item, i) => {
        if (i === index) {
            item.classList.add("active");
            item.scrollIntoView({ block: "nearest" });
        } else {
            item.classList.remove("active");
        }
    });
}

// --------------------------------------------------------------------------
// RECENT & TRENDING SEARCHES ENGINE
// --------------------------------------------------------------------------
function getRecentSearches(scope) {
    try {
        const stored = localStorage.getItem(`tga_recent_searches_${scope}`);
        return stored ? JSON.parse(stored) : [];
    } catch {
        return [];
    }
}

function saveRecentSearch(scope, query) {
    if (!query || query.trim().length < 2) return;
    try {
        const list = getRecentSearches(scope).filter(x => x.toLowerCase() !== query.toLowerCase());
        list.unshift(query.trim());
        if (list.length > 6) list.pop();
        localStorage.setItem(`tga_recent_searches_${scope}`, JSON.stringify(list));
    } catch {}
}

function clearRecentSearches(scope, dropdown, input) {
    try {
        localStorage.removeItem(`tga_recent_searches_${scope}`);
        renderRecentAndTrending(dropdown, scope, input);
    } catch {}
}

function renderRecentAndTrending(dropdown, scope, input) {
    const recents = getRecentSearches(scope);
    const trending = [
        "Chyawanprash", "Ashwagandha", "Triphala", "Kumkumadi Tailam", 
        "A2 Cow Ghee", "Giloy Ghanvati", "Hair Fall Oil"
    ];

    if (!recents.length && !trending.length) {
        dropdown.classList.remove("show");
        return;
    }

    let html = `<div class="p-3">`;

    if (recents.length > 0) {
        html += `
            <div class="d-flex justify-content-between align-items-center mb-2">
                <span class="text-uppercase text-xs fw-bold text-muted"><i class="bi bi-clock-history me-1"></i> Recent Searches</span>
                <button type="button" class="btn btn-link p-0 text-xs text-danger text-decoration-none btn-clear-recents">Clear All</button>
            </div>
            <div class="d-flex flex-wrap gap-2 mb-3">
        `;
        recents.forEach(r => {
            html += `<span class="search-recent-chip" data-search-term="${escapeHtml(r)}"><i class="bi bi-clock text-muted"></i> ${escapeHtml(r)}</span>`;
        });
        html += `</div>`;
    }

    html += `
        <div class="mb-2">
            <span class="text-uppercase text-xs fw-bold text-success"><i class="bi bi-graph-up-arrow me-1"></i> Trending Ayurveda Searches</span>
        </div>
        <div class="d-flex flex-wrap gap-2">
    `;
    trending.forEach(t => {
        html += `<span class="search-trending-chip" data-search-term="${escapeHtml(t)}"><i class="bi bi-fire text-warning"></i> ${escapeHtml(t)}</span>`;
    });
    html += `</div></div>`;

    dropdown.innerHTML = html;
    dropdown.classList.add("show");

    // Wire chips
    dropdown.querySelectorAll("[data-search-term]").forEach(chip => {
        chip.addEventListener("click", function () {
            const term = chip.getAttribute("data-search-term");
            if (input) {
                input.value = term;
                saveRecentSearch(scope, term);
                if (scope === "admin") {
                    window.location.href = `/Admin/Products?query=${encodeURIComponent(term)}`;
                } else {
                    window.location.href = `/search?q=${encodeURIComponent(term)}`;
                }
            }
        });
    });

    const clearRecentsBtn = dropdown.querySelector(".btn-clear-recents");
    if (clearRecentsBtn) {
        clearRecentsBtn.addEventListener("click", function (e) {
            e.stopPropagation();
            clearRecentSearches(scope, dropdown, input);
        });
    }
}

// --------------------------------------------------------------------------
// HIGHLIGHT MATCHED TEXT SAFELY
// --------------------------------------------------------------------------
function highlightMatch(text, query) {
    if (!text) return "";
    if (!query) return escapeHtml(text);

    const safeText = escapeHtml(text);
    const escapedQuery = query.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
    const regex = new RegExp(`(${escapedQuery})`, "gi");

    return safeText.replace(regex, `<mark class="tga-match-highlight">$1</mark>`);
}

// --------------------------------------------------------------------------
// RENDER SEARCH SUGGESTIONS (PRODUCTS, CATEGORIES, HERBS, DOCTORS, ADMIN)
// --------------------------------------------------------------------------
function renderSuggestions(data, dropdown, query, scope, input) {
    if (!data || data.totalSuggestions === 0) {
        let noResultsHtml = `
            <div class="p-4 text-center">
                <i class="bi bi-search text-muted fs-3 mb-2 d-block"></i>
                <div class="fw-semibold text-dark mb-1">No Ayurvedic formulations found for "${escapeHtml(query)}"</div>
                <div class="text-muted small mb-3">Try checking spelling or explore our classical Dravyas:</div>
                <div class="d-flex flex-wrap justify-content-center gap-2">
                    <span class="search-trending-chip" data-search-term="Chyawanprash">Chyawanprash</span>
                    <span class="search-trending-chip" data-search-term="Ashwagandha">Ashwagandha</span>
                    <span class="search-trending-chip" data-search-term="Triphala">Triphala</span>
                    <span class="search-trending-chip" data-search-term="Kumkumadi">Kumkumadi</span>
                </div>
            </div>
        `;
        dropdown.innerHTML = noResultsHtml;
        dropdown.classList.add("show");

        dropdown.querySelectorAll("[data-search-term]").forEach(chip => {
            chip.addEventListener("click", function () {
                const term = chip.getAttribute("data-search-term");
                input.value = term;
                saveRecentSearch(scope, term);
                window.location.href = `/search?q=${encodeURIComponent(term)}`;
            });
        });
        return;
    }

    let html = "";

    // 1. Did You Mean typo banner
    if (data.didYouMean) {
        html += `
            <div class="p-2 px-3 bg-warning-subtle border-bottom d-flex align-items-center justify-content-between">
                <div class="small text-dark">
                    <i class="bi bi-lightbulb-fill text-warning me-1"></i> Did you mean: 
                    <a href="/search?q=${encodeURIComponent(data.didYouMean)}" class="fw-bold text-success text-decoration-none btn-did-you-mean" data-term="${escapeHtml(data.didYouMean)}">${escapeHtml(data.didYouMean)}</a>?
                </div>
            </div>
        `;
    }

    // 2. Hindi / Hinglish Synonym Applied banner
    if (data.synonymApplied) {
        html += `
            <div class="p-2 px-3 bg-success-subtle border-bottom d-flex align-items-center">
                <div class="small text-success-emphasis">
                    <i class="bi bi-translate me-1"></i> Ayurvedic Synonym: 
                    <strong>${escapeHtml(data.synonymApplied)}</strong>
                </div>
            </div>
        `;
    }

    // --- SCOPE: PUBLIC ---
    if (scope !== "admin") {
        // Products
        if (data.products && data.products.length) {
            html += `<div class="search-section-header"><i class="bi bi-capsule me-1"></i> Formulations &amp; Medicines (${data.products.length})</div>`;
            data.products.forEach(p => {
                html += `
                    <a href="${p.url}" class="search-item" data-search-item>
                        <img src="${p.imageUrl || '/images/products/chyawanprash.svg'}" alt="" width="36" height="36" class="rounded object-fit-contain bg-light p-1" />
                        <div class="flex-grow-1 overflow-hidden">
                            <div class="text-truncate fw-semibold small">${highlightMatch(p.title, query)}</div>
                            <div class="text-muted text-xs">${escapeHtml(p.subtitle || '')}</div>
                        </div>
                        <div class="text-end">
                            <div class="fw-bold text-success small">₹${p.price || 0}</div>
                            ${p.badge ? `<span class="badge bg-light text-dark border text-xs">${escapeHtml(p.badge)}</span>` : ''}
                        </div>
                    </a>
                `;
            });
        }

        // Classical Ayurvedic Herbs
        if (data.herbs && data.herbs.length) {
            html += `<div class="search-section-header"><i class="bi bi-flower1 me-1"></i> Classical Ayurvedic Herbs (${data.herbs.length})</div>`;
            data.herbs.forEach(h => {
                html += `
                    <a href="${h.url}" class="search-item" data-search-item>
                        <i class="bi bi-tree text-success fs-5"></i>
                        <div class="flex-grow-1 overflow-hidden">
                            <div class="text-truncate fw-semibold small">${highlightMatch(h.title, query)}</div>
                            <div class="text-muted text-xs text-truncate">${escapeHtml(h.subtitle || '')}</div>
                        </div>
                        <span class="badge bg-success-subtle text-success text-xs">Herb</span>
                    </a>
                `;
            });
        }

        // Categories
        if (data.categories && data.categories.length) {
            html += `<div class="search-section-header"><i class="bi bi-tag me-1"></i> Categories (${data.categories.length})</div>`;
            data.categories.forEach(c => {
                html += `
                    <a href="${c.url}" class="search-item" data-search-item>
                        <i class="bi bi-grid text-muted fs-5"></i>
                        <div class="flex-grow-1 overflow-hidden">
                            <div class="text-truncate fw-semibold small">${highlightMatch(c.title, query)}</div>
                            <div class="text-muted text-xs">${escapeHtml(c.subtitle || '')}</div>
                        </div>
                        <span class="badge bg-light text-secondary text-xs">Explore</span>
                    </a>
                `;
            });
        }

        // Health Concerns & Doshas
        if (data.concerns && data.concerns.length) {
            html += `<div class="search-section-header"><i class="bi bi-heart-pulse me-1"></i> Health Concerns (${data.concerns.length})</div>`;
            data.concerns.forEach(cn => {
                html += `
                    <a href="${cn.url}" class="search-item" data-search-item>
                        <i class="bi bi-activity text-danger fs-5"></i>
                        <div class="flex-grow-1 overflow-hidden">
                            <div class="text-truncate fw-semibold small">${highlightMatch(cn.title, query)}</div>
                            <div class="text-muted text-xs text-truncate">${escapeHtml(cn.subtitle || '')}</div>
                        </div>
                        <span class="badge bg-danger-subtle text-danger text-xs">Concern</span>
                    </a>
                `;
            });
        }

        // Verified Doctors
        if (data.doctors && data.doctors.length) {
            html += `<div class="search-section-header"><i class="bi bi-person-badge me-1"></i> AYUSH Vaidyas (${data.doctors.length})</div>`;
            data.doctors.forEach(d => {
                html += `
                    <a href="${d.url}" class="search-item" data-search-item>
                        <i class="bi bi-person-circle fs-4 text-success"></i>
                        <div class="flex-grow-1 overflow-hidden">
                            <div class="text-truncate fw-semibold small">${highlightMatch(d.title, query)}</div>
                            <div class="text-muted text-xs">${escapeHtml(d.subtitle || '')}</div>
                        </div>
                        <span class="badge bg-success text-white text-xs">Consult</span>
                    </a>
                `;
            });
        }
    } else {
        // --- SCOPE: ADMIN ---
        if (data.adminOrders && data.adminOrders.length) {
            html += `<div class="search-section-header"><i class="bi bi-receipt me-1"></i> Orders (${data.adminOrders.length})</div>`;
            data.adminOrders.forEach(o => {
                html += `
                    <a href="${o.url}" class="search-item" data-search-item>
                        <i class="bi bi-box-seam text-primary fs-5"></i>
                        <div class="flex-grow-1 overflow-hidden">
                            <div class="text-truncate fw-semibold small">${highlightMatch(o.title, query)}</div>
                            <div class="text-muted text-xs">${escapeHtml(o.subtitle || '')}</div>
                        </div>
                        <span class="badge bg-primary-subtle text-primary text-xs">${escapeHtml(o.badge || '')}</span>
                    </a>
                `;
            });
        }

        if (data.adminUsers && data.adminUsers.length) {
            html += `<div class="search-section-header"><i class="bi bi-people me-1"></i> Users &amp; Patients (${data.adminUsers.length})</div>`;
            data.adminUsers.forEach(u => {
                html += `
                    <a href="${u.url}" class="search-item" data-search-item>
                        <i class="bi bi-person text-secondary fs-5"></i>
                        <div class="flex-grow-1 overflow-hidden">
                            <div class="text-truncate fw-semibold small">${highlightMatch(u.title, query)}</div>
                            <div class="text-muted text-xs">${escapeHtml(u.subtitle || '')}</div>
                        </div>
                        <span class="badge bg-secondary-subtle text-secondary text-xs">User</span>
                    </a>
                `;
            });
        }

        if (data.adminProducts && data.adminProducts.length) {
            html += `<div class="search-section-header"><i class="bi bi-boxes me-1"></i> Products Stock (${data.adminProducts.length})</div>`;
            data.adminProducts.forEach(p => {
                html += `
                    <a href="${p.url}" class="search-item" data-search-item>
                        <i class="bi bi-box text-success fs-5"></i>
                        <div class="flex-grow-1 overflow-hidden">
                            <div class="text-truncate fw-semibold small">${highlightMatch(p.title, query)}</div>
                            <div class="text-muted text-xs">${escapeHtml(p.subtitle || '')}</div>
                        </div>
                        <span class="badge ${p.badge === 'In Stock' ? 'bg-success-subtle text-success' : 'bg-danger-subtle text-danger'} text-xs">${escapeHtml(p.badge || '')}</span>
                    </a>
                `;
            });
        }

        if (data.doctors && data.doctors.length) {
            html += `<div class="search-section-header"><i class="bi bi-person-check me-1"></i> Doctors (${data.doctors.length})</div>`;
            data.doctors.forEach(d => {
                html += `
                    <a href="${d.url}" class="search-item" data-search-item>
                        <i class="bi bi-heart-pulse text-info fs-5"></i>
                        <div class="flex-grow-1 overflow-hidden">
                            <div class="text-truncate fw-semibold small">${highlightMatch(d.title, query)}</div>
                            <div class="text-muted text-xs">${escapeHtml(d.subtitle || '')}</div>
                        </div>
                        <span class="badge bg-info-subtle text-info text-xs">${escapeHtml(d.badge || '')}</span>
                    </a>
                `;
            });
        }
    }

    // View all results footer link & latency indicator
    html += `
        <div class="p-2 px-3 bg-light d-flex justify-content-between align-items-center border-top">
            <a href="/search?q=${encodeURIComponent(query)}" class="small fw-semibold text-success text-decoration-none">
                <i class="bi bi-search me-1"></i> See all results for "${escapeHtml(query)}" &rarr;
            </a>
            <span class="text-muted text-xs">⚡ ${data.latencyMs}ms</span>
        </div>
    `;

    dropdown.innerHTML = html;
    dropdown.classList.add("show");

    // Track search click into recent searches
    dropdown.querySelectorAll("[data-search-item]").forEach(item => {
        item.addEventListener("click", function () {
            saveRecentSearch(scope, query);
        });
    });

    const didYouMeanBtn = dropdown.querySelector(".btn-did-you-mean");
    if (didYouMeanBtn) {
        didYouMeanBtn.addEventListener("click", function (e) {
            e.preventDefault();
            const term = didYouMeanBtn.getAttribute("data-term");
            if (input) {
                input.value = term;
                saveRecentSearch(scope, term);
                window.location.href = `/search?q=${encodeURIComponent(term)}`;
            }
        });
    }
}


// --------------------------------------------------------------------------
// 2. REUSABLE ACCESSIBLE CONFIRMATION & LOGOUT MODAL (PHASE 9)
// --------------------------------------------------------------------------
function initConfirmModal() {
    const modalEl = document.getElementById("confirmActionModal");
    if (!modalEl) return;

    const modalTitleText = document.getElementById("confirmModalTitleText") || document.getElementById("confirmModalTitle");
    const modalBody = document.getElementById("confirmModalBody");
    const modalForm = document.getElementById("confirmModalForm");
    const proceedBtn = document.getElementById("confirmModalProceedBtn");
    const cancelBtn = document.getElementById("confirmModalCancelBtn");
    const iconEl = document.getElementById("confirmModalIcon");

    let lastFocusedElement = null;
    const bsModal = new bootstrap.Modal(modalEl);

    // Focus management & accessible focus trap
    modalEl.addEventListener("shown.bs.modal", function () {
        if (cancelBtn) cancelBtn.focus();
    });

    modalEl.addEventListener("hidden.bs.modal", function () {
        if (lastFocusedElement && typeof lastFocusedElement.focus === "function") {
            lastFocusedElement.focus();
        }
    });

    modalEl.addEventListener("keydown", function (e) {
        if (e.key === "Tab") {
            const focusables = modalEl.querySelectorAll("button, [href], input, select, textarea, [tabindex]:not([tabindex='-1'])");
            if (focusables.length === 0) return;
            const first = focusables[0];
            const last = focusables[focusables.length - 1];

            if (e.shiftKey) {
                if (document.activeElement === first) {
                    last.focus();
                    e.preventDefault();
                }
            } else {
                if (document.activeElement === last) {
                    first.focus();
                    e.preventDefault();
                }
            }
        } else if (e.key === "Enter" && document.activeElement !== cancelBtn) {
            e.preventDefault();
            if (modalForm) modalForm.submit();
        }
    });

    // 1. Logout Triggers
    const logoutTriggers = document.querySelectorAll("[data-tga-logout], .btn-logout-trigger");
    logoutTriggers.forEach(btn => {
        btn.addEventListener("click", function (e) {
            e.preventDefault();
            lastFocusedElement = btn;

            const actionUrl = btn.getAttribute("data-action-url") || "/Account/Logout";

            if (modalTitleText) modalTitleText.innerText = "Confirm Sign Out";
            if (modalBody) modalBody.innerText = "Are you sure you want to log out of your session?";
            if (proceedBtn) {
                proceedBtn.innerText = "Log out";
                proceedBtn.className = "btn btn-danger px-4 rounded-pill fw-semibold";
            }
            if (iconEl) {
                iconEl.className = "bi bi-box-arrow-right text-danger fs-4";
            }
            if (modalForm) {
                modalForm.setAttribute("action", actionUrl);
                modalForm.setAttribute("method", "post");
            }

            bsModal.show();
        });
    });

    // 2. Generic Destructive Confirmation Triggers (e.g. Delete Address, Cancel Order)
    const confirmTriggers = document.querySelectorAll("[data-confirm-action]");
    confirmTriggers.forEach(btn => {
        btn.addEventListener("click", function (e) {
            e.preventDefault();
            lastFocusedElement = btn;

            const title = btn.getAttribute("data-confirm-title") || "Confirm Action";
            const body = btn.getAttribute("data-confirm-body") || "Are you sure you want to proceed?";
            const proceedText = btn.getAttribute("data-confirm-btn-text") || "Confirm";
            const btnClass = btn.getAttribute("data-confirm-btn-class") || "btn-danger";
            const actionUrl = btn.getAttribute("data-action-url") || "";
            const method = btn.getAttribute("data-action-method") || "post";

            if (modalTitleText) modalTitleText.innerText = title;
            if (modalBody) modalBody.innerText = body;
            if (proceedBtn) {
                proceedBtn.innerText = proceedText;
                proceedBtn.className = `btn ${btnClass} px-4 rounded-pill fw-semibold`;
            }
            if (iconEl) {
                iconEl.className = "bi bi-exclamation-triangle-fill text-warning fs-4";
            }
            if (modalForm) {
                modalForm.setAttribute("action", actionUrl);
                modalForm.setAttribute("method", method);
            }

            bsModal.show();
        });
    });
}

// --------------------------------------------------------------------------
// 2b. SESSION INACTIVITY / IDLE TIMEOUT WARNING MODAL (PHASE 9 - Doctor Portal)
// --------------------------------------------------------------------------
function initIdleTimeoutModal() {
    const idleModalEl = document.getElementById("idleTimeoutModal");
    if (!idleModalEl) return;

    const countdownEl = document.getElementById("idleCountdownTimer");
    const staySignedBtn = document.getElementById("btnStaySignedIn");
    const idleForm = document.getElementById("idleLogoutForm");

    const idleTimeoutLimit = window.TGA_IDLE_TIMEOUT_MS || (25 * 60 * 1000);
    const countdownSeconds = window.TGA_IDLE_COUNTDOWN_SEC || 60;

    let idleTimer = null;
    let countdownInterval = null;
    let remainingSeconds = countdownSeconds;
    const bsIdleModal = new bootstrap.Modal(idleModalEl, {
        backdrop: 'static',
        keyboard: false
    });

    function resetIdleTimer() {
        if (countdownInterval) return; // Inactivity modal is actively open

        clearTimeout(idleTimer);
        idleTimer = setTimeout(showInactivityWarning, idleTimeoutLimit);
    }

    function showInactivityWarning() {
        remainingSeconds = countdownSeconds;
        if (countdownEl) countdownEl.innerText = remainingSeconds;

        bsIdleModal.show();
        if (staySignedBtn) staySignedBtn.focus();

        countdownInterval = setInterval(() => {
            remainingSeconds--;
            if (countdownEl) countdownEl.innerText = remainingSeconds;

            if (remainingSeconds <= 0) {
                clearInterval(countdownInterval);
                countdownInterval = null;
                if (idleForm) {
                    idleForm.submit();
                } else {
                    window.location.href = "/Account/Login";
                }
            }
        }, 1000);
    }

    if (staySignedBtn) {
        staySignedBtn.addEventListener("click", function () {
            clearInterval(countdownInterval);
            countdownInterval = null;
            bsIdleModal.hide();
            resetIdleTimer();

            fetch("/Home/Ping", { method: "HEAD" }).catch(() => {});
        });
    }

    const activityEvents = ["mousemove", "mousedown", "keydown", "touchstart", "scroll"];
    let lastActivity = Date.now();

    activityEvents.forEach(evt => {
        window.addEventListener(evt, () => {
            const now = Date.now();
            if (now - lastActivity > 1000) {
                lastActivity = now;
                resetIdleTimer();
            }
        }, { passive: true });
    });

    resetIdleTimer();
}

// --------------------------------------------------------------------------
// 3. AJAX ADD-TO-CART ENGINE
// --------------------------------------------------------------------------
function initAjaxCart() {
    const addButtons = document.querySelectorAll(".btn-add-to-cart-ajax");

    addButtons.forEach(btn => {
        btn.addEventListener("click", function (e) {
            e.preventDefault();
            const productId = btn.getAttribute("data-product-id");
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

            // Visual feedback on button
            const originalHtml = btn.innerHTML;
            btn.disabled = true;
            btn.innerHTML = `<span class="spinner-border spinner-border-sm me-1" role="status"></span> Adding...`;

            fetch("/Cart/AddToCartAjax", {
                method: "POST",
                headers: {
                    "Content-Type": "application/x-www-form-urlencoded",
                    "RequestVerificationToken": token || ""
                },
                body: `productId=${encodeURIComponent(productId)}&quantity=1`
            })
            .then(res => res.json())
            .then(data => {
                btn.disabled = false;
                btn.innerHTML = originalHtml;

                if (data.success) {
                    const badge = document.getElementById("cartCountBadge");
                    if (badge) {
                        badge.innerText = data.cartCount;
                        badge.classList.add("scale-up");
                        setTimeout(() => badge.classList.remove("scale-up"), 300);
                    }
                    showToast("Added to Cart", data.message || "Item added to your basket.");
                } else {
                    showToast("Notice", data.message || "Unable to add item.", "warning");
                }
            })
            .catch(err => {
                btn.disabled = false;
                btn.innerHTML = originalHtml;
                console.error("Cart error:", err);
                showToast("Error", "Could not add item to basket. Please try again.", "danger");
            });
        });
    });
}

// --------------------------------------------------------------------------
// 4. PRAKRITI DOSHA QUIZ (5-QUESTION INTERACTIVE ENGINE)
// --------------------------------------------------------------------------
function initPrakritiQuiz() {
    const quizCard = document.getElementById("prakritiQuizSection");
    if (!quizCard) return;

    const questions = [
        {
            q: "1. What best describes your physical frame and body build?",
            options: [
                { text: "Slender, light bones, hard to gain weight", dosha: "vata" },
                { text: "Medium, athletic, well-proportioned, moderate weight", dosha: "pitta" },
                { text: "Solid, broad build, gains weight easily, slow metabolism", dosha: "kapha" }
            ]
        },
        {
            q: "2. How does your skin typically feel across changing seasons?",
            options: [
                { text: "Dry, rough, tends towards chapping in cold wind", dosha: "vata" },
                { text: "Warm, reddish, sensitive, prone to freckles or breakouts", dosha: "pitta" },
                { text: "Smooth, thick, soft, naturally moisturized, slightly oily", dosha: "kapha" }
            ]
        },
        {
            q: "3. What is your typical digestive pattern and appetite (Agni)?",
            options: [
                { text: "Variable, irregular hunger, prone to gas or bloating", dosha: "vata" },
                { text: "Sharp, strong appetite, cannot tolerate skipping meals", dosha: "pitta" },
                { text: "Slow, steady hunger, easily skips meals without distress", dosha: "kapha" }
            ]
        },
        {
            q: "4. How is your regular sleep pattern?",
            options: [
                { text: "Light, easily awakened, dreams of movement and flying", dosha: "vata" },
                { text: "Moderate, 6-7 hours, can wake up energized quickly", dosha: "pitta" },
                { text: "Deep, heavy sleep, difficulty waking up in the morning", dosha: "kapha" }
            ]
        },
        {
            q: "5. What is your natural emotional response under acute stress?",
            options: [
                { text: "Anxious, racing thoughts, restlessness, worry", dosha: "vata" },
                { text: "Irritable, intense, impatient, competitive drive", dosha: "pitta" },
                { text: "Calm, withdraws, resistant to sudden change, passive", dosha: "kapha" }
            ]
        }
    ];

    let currentStep = 0;
    const answers = [];

    const stepCounterEl = document.getElementById("quizStepCounter");
    const questionTextEl = document.getElementById("quizQuestionText");
    const optionsContainerEl = document.getElementById("quizOptionsContainer");
    const progressBarEl = document.getElementById("quizProgressBar");
    const resultContainerEl = document.getElementById("quizResultContainer");
    const quizBodyEl = document.getElementById("quizBodyContainer");

    function renderStep() {
        if (!stepCounterEl || !questionTextEl || !optionsContainerEl) return;

        const currentQ = questions[currentStep];
        stepCounterEl.innerText = `Question ${currentStep + 1} of ${questions.length}`;
        questionTextEl.innerText = currentQ.q;
        if (progressBarEl) {
            progressBarEl.style.width = `${((currentStep + 1) / questions.length) * 100}%`;
        }

        let optionsHtml = "";
        currentQ.options.forEach((opt, idx) => {
            optionsHtml += `
                <button type="button" class="prakriti-option-btn w-100 mb-2" data-dosha="${opt.dosha}">
                    <span class="badge bg-light text-dark border px-2 py-1">${String.fromCharCode(65 + idx)}</span>
                    <span>${escapeHtml(opt.text)}</span>
                </button>
            `;
        });
        optionsContainerEl.innerHTML = optionsHtml;

        optionsContainerEl.querySelectorAll(".prakriti-option-btn").forEach(btn => {
            btn.addEventListener("click", function () {
                const dosha = btn.getAttribute("data-dosha");
                answers[currentStep] = dosha;
                currentStep++;
                if (currentStep < questions.length) {
                    renderStep();
                } else {
                    calculateAndShowResult();
                }
            });
        });
    }

    function calculateAndShowResult() {
        const counts = { vata: 0, pitta: 0, kapha: 0 };
        answers.forEach(d => {
            if (counts[d] !== undefined) counts[d]++;
        });

        let dominant = "vata";
        if (counts.pitta > counts[dominant]) dominant = "pitta";
        if (counts.kapha > counts[dominant]) dominant = "kapha";

        const vataPct = Math.round((counts.vata / answers.length) * 100);
        const pittaPct = Math.round((counts.pitta / answers.length) * 100);
        const kaphaPct = Math.round((counts.kapha / answers.length) * 100);

        const details = {
            vata: {
                title: "Vata Predominant (Air & Space)",
                desc: "Your constitution is governed by Prana, vitality, and swift movement. When balanced, you are creative, vibrant, and intuitive.",
                ahara: "Warm, nourishing, gently spiced soups, cooked grains, and healthy oils like pure A2 Cow Ghee and warm sesame oil.",
                herbs: "Ashwagandha, Shatavari, Dashamula, and warm ginger infusions.",
                medicineUrl: "/Shop?concern=Stress",
                medicineName: "Ashwagandha & Rasayana Herbal Tonics"
            },
            pitta: {
                title: "Pitta Predominant (Fire & Water)",
                desc: "Your constitution is governed by Agni, metabolic digestion, and sharp intellect. When balanced, you are focused, articulate, and warm.",
                ahara: "Cooling sweet fruits, fresh greens, cilantro, coconut water, and soothing herbs. Avoid excessive chili and pungent spices.",
                herbs: "Amalaki (Amla), Brahmi, Guduchi, and pure Chandan.",
                medicineUrl: "/Shop?concern=Digestion",
                medicineName: "Amalaki & Pitta Balancing Tonics"
            },
            kapha: {
                title: "Kapha Predominant (Earth & Water)",
                desc: "Your constitution is governed by stability, endurance, and deep compassion. When balanced, you have immense stamina and emotional grounding.",
                ahara: "Light, warm, pungent and bitter foods. Black pepper, ginger, leafy greens, and warm honey water in the morning.",
                herbs: "Triphala, Tulsi, Trikatu, and Guggulu.",
                medicineUrl: "/Shop?concern=Metabolism",
                medicineName: "Triphala & Agni Metabolism Powders"
            }
        };

        const res = details[dominant];

        if (quizBodyEl) quizBodyEl.classList.add("d-none");
        if (resultContainerEl) {
            resultContainerEl.classList.remove("d-none");
            resultContainerEl.innerHTML = `
                <div class="text-center mb-4">
                    <span class="badge bg-warning text-dark px-3 py-2 fw-bold text-uppercase mb-2">Assessment Result</span>
                    <h3 class="fw-bold font-serif text-success mb-2">${res.title}</h3>
                    <p class="text-muted small">${res.desc}</p>
                </div>

                <div class="card bg-light border-0 p-3 mb-4 rounded-3">
                    <div class="d-flex justify-content-between small fw-bold mb-1">
                        <span>Vata: ${vataPct}%</span>
                        <span>Pitta: ${pittaPct}%</span>
                        <span>Kapha: ${kaphaPct}%</span>
                    </div>
                    <div class="progress" style="height: 10px;">
                        <div class="progress-bar bg-info" style="width: ${vataPct}%;"></div>
                        <div class="progress-bar bg-warning" style="width: ${pittaPct}%;"></div>
                        <div class="progress-bar bg-success" style="width: ${kaphaPct}%;"></div>
                    </div>
                </div>

                <div class="row g-3 mb-4">
                    <div class="col-md-6">
                        <div class="p-3 bg-white border rounded-3 h-100">
                            <h6 class="fw-bold text-success mb-2"><i class="bi bi-cup-hot me-1"></i> Recommended Ahara (Diet)</h6>
                            <p class="small text-muted mb-0">${res.ahara}</p>
                        </div>
                    </div>
                    <div class="col-md-6">
                        <div class="p-3 bg-white border rounded-3 h-100">
                            <h6 class="fw-bold text-success mb-2"><i class="bi bi-flower1 me-1"></i> Classical Dravya (Herbs)</h6>
                            <p class="small text-muted mb-0">${res.herbs}</p>
                        </div>
                    </div>
                </div>

                <div class="alert alert-warning py-2 px-3 small border-0 rounded-3 mb-4">
                    <i class="bi bi-info-circle me-1"></i>
                    <strong>Statutory Educational Disclaimer:</strong> This assessment is an Ayurvedic wellness guide, not a formal clinical diagnosis. Consult a qualified Vaidya for comprehensive Nadi Pariksha.
                </div>

                <div class="d-flex flex-wrap gap-3 justify-content-center">
                    <a href="${res.medicineUrl}" class="btn btn-tga-gold">
                        <i class="bi bi-bag me-1"></i> Browse ${res.medicineName}
                    </a>
                    <a href="/Doctors" class="btn btn-tga-outline">
                        <i class="bi bi-camera-video me-1"></i> Consult Vaidya for Nadi Pariksha
                    </a>
                    <button type="button" class="btn btn-outline-secondary rounded-pill px-4" id="retakeQuizBtn">
                        <i class="bi bi-arrow-counterclockwise me-1"></i> Retake Quiz
                    </button>
                </div>
            `;

            document.getElementById("retakeQuizBtn")?.addEventListener("click", function () {
                currentStep = 0;
                answers.length = 0;
                resultContainerEl.classList.add("d-none");
                quizBodyEl.classList.remove("d-none");
                renderStep();
            });
        }
    }

    renderStep();
}

// --------------------------------------------------------------------------
// 5. DEALS STRIP COUNTDOWN TIMER (HH:MM:SS)
// --------------------------------------------------------------------------
function initCountdownTimer() {
    const hoursEl = document.getElementById("timerHours");
    const minsEl = document.getElementById("timerMins");
    const secsEl = document.getElementById("timerSecs");
    if (!hoursEl || !minsEl || !secsEl) return;

    // Countdown 8 hours from daily reset
    let totalSeconds = (8 * 3600) + (45 * 60) + 30;

    setInterval(() => {
        if (totalSeconds <= 0) {
            totalSeconds = 86400; // Reset
        }
        totalSeconds--;

        const h = Math.floor(totalSeconds / 3600);
        const m = Math.floor((totalSeconds % 3600) / 60);
        const s = totalSeconds % 60;

        hoursEl.innerText = String(h).padStart(2, "0");
        minsEl.innerText = String(m).padStart(2, "0");
        secsEl.innerText = String(s).padStart(2, "0");
    }, 1000);
}

// --------------------------------------------------------------------------
// 6. ACCESSIBLE TOUCH & KEYBOARD CAROUSEL
// --------------------------------------------------------------------------
function initTouchCarousel() {
    const track = document.getElementById("featuredCarouselTrack");
    const prevBtn = document.getElementById("carouselPrevBtn");
    const nextBtn = document.getElementById("carouselNextBtn");
    if (!track) return;

    if (prevBtn) {
        prevBtn.addEventListener("click", () => {
            track.scrollBy({ left: -320, behavior: "smooth" });
        });
    }

    if (nextBtn) {
        nextBtn.addEventListener("click", () => {
            track.scrollBy({ left: 320, behavior: "smooth" });
        });
    }

    // Keyboard support
    track.addEventListener("keydown", (e) => {
        if (e.key === "ArrowLeft") {
            e.preventDefault();
            track.scrollBy({ left: -320, behavior: "smooth" });
        } else if (e.key === "ArrowRight") {
            e.preventDefault();
            track.scrollBy({ left: 320, behavior: "smooth" });
        }
    });
}

// --------------------------------------------------------------------------
// 7. DINACHARYA (DAILY ROUTINE) CLOCK TABS
// --------------------------------------------------------------------------
function initDinacharyaTabs() {
    const tabBtns = document.querySelectorAll(".dinacharya-btn");
    const cards = document.querySelectorAll(".dinacharya-card");
    if (!tabBtns.length) return;

    tabBtns.forEach(btn => {
        btn.addEventListener("click", () => {
            tabBtns.forEach(b => b.classList.remove("active"));
            cards.forEach(c => c.classList.add("d-none"));

            btn.classList.add("active");
            const targetId = btn.getAttribute("data-target");
            const targetCard = document.getElementById(targetId);
            if (targetCard) targetCard.classList.remove("d-none");
        });
    });
}

// --------------------------------------------------------------------------
// 8. SCROLL REVEAL (INTERSECTION OBSERVER)
// --------------------------------------------------------------------------
function initScrollReveal() {
    if (!("IntersectionObserver" in window)) return;

    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.classList.add("is-revealed");
                observer.unobserve(entry.target);
            }
        });
    }, {
        threshold: 0.12,
        rootMargin: "0px 0px -40px 0px"
    });

    document.querySelectorAll(".reveal-on-scroll").forEach(el => observer.observe(el));
}

// --------------------------------------------------------------------------
// 9. COUPON COPY TO CLIPBOARD
// --------------------------------------------------------------------------
function initCouponCopy() {
    const copyBtns = document.querySelectorAll("[data-copy-coupon]");
    copyBtns.forEach(btn => {
        btn.addEventListener("click", function () {
            const code = btn.getAttribute("data-copy-coupon") || "AYUSH15";
            if (navigator.clipboard) {
                navigator.clipboard.writeText(code).then(() => {
                    showToast("Coupon Copied", `Code ${code} copied to clipboard! Enjoy your discount.`);
                });
            } else {
                showToast("Coupon Code", `Use code ${code} at checkout.`);
            }
        });
    });
}

// --------------------------------------------------------------------------
// TOAST & HTML HELPERS
// --------------------------------------------------------------------------
function showToast(title, message, type = "success") {
    const toastContainer = document.getElementById("tgaToastContainer");
    if (!toastContainer) return;

    const toastEl = document.createElement("div");
    toastEl.className = `toast align-items-center text-bg-${type === "warning" ? "warning" : type === "danger" ? "danger" : "dark"} border-0 shadow-lg`;
    toastEl.setAttribute("role", "alert");
    toastEl.setAttribute("aria-live", "assertive");
    toastEl.setAttribute("aria-atomic", "true");
    toastEl.innerHTML = `
        <div class="d-flex">
            <div class="toast-body">
                <strong>${escapeHtml(title)}:</strong> ${escapeHtml(message)}
            </div>
            <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
        </div>
    `;
    toastContainer.appendChild(toastEl);
    const toast = new bootstrap.Toast(toastEl, { delay: 3500 });
    toast.show();
    toastEl.addEventListener("hidden.bs.toast", () => toastEl.remove());
}

function escapeHtml(string) {
    if (!string) return "";
    return String(string)
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#039;");
}
