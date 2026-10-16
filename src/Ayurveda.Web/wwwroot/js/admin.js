// Admin Smart Search & Interactions Engine
document.addEventListener("DOMContentLoaded", function () {
    initAdminSearchBox();
    initConfirmModal();
    initIdleTimeoutModal();
    initThemeToggle();
    initSidebarToggle();
    initKpiCounterAnimation();
    initDashboardCharts();
});

function initAdminSearchBox() {
    const searchInputs = document.querySelectorAll(".tga-search-input");

    searchInputs.forEach(input => {
        const container = input.closest(".search-container");
        if (!container) return;

        const dropdown = container.querySelector(".search-results-dropdown");
        const clearBtn = container.querySelector(".btn-clear-search");
        const form = container.querySelector("form");
        if (!dropdown) return;

        const scope = input.getAttribute("data-scope") || "admin";
        let debounceTimer = null;
        let abortController = null;
        let selectedIndex = -1;

        function toggleClearBtn() {
            if (clearBtn) {
                if (input.value.trim().length > 0) {
                    clearBtn.classList.remove("d-none");
                } else {
                    clearBtn.classList.add("d-none");
                }
            }
        }

        if (clearBtn) {
            clearBtn.addEventListener("click", function () {
                input.value = "";
                toggleClearBtn();
                dropdown.classList.remove("show");
                input.focus();
            });
        }

        input.addEventListener("input", function () {
            toggleClearBtn();
            clearTimeout(debounceTimer);
            const query = input.value.trim();

            if (query.length < 2) {
                dropdown.innerHTML = "";
                dropdown.classList.remove("show");
                return;
            }

            debounceTimer = setTimeout(() => {
                if (abortController) {
                    abortController.abort();
                }
                abortController = new AbortController();

                dropdown.innerHTML = `<div class="p-3 text-muted small text-center"><span class="spinner-border spinner-border-sm me-2 text-success"></span>Searching admin records...</div>`;
                dropdown.classList.add("show");

                fetch(`/search/suggest?q=${encodeURIComponent(query)}&scope=admin`, {
                    signal: abortController.signal
                })
                .then(res => {
                    if (!res.ok) throw new Error("Search error");
                    return res.json();
                })
                .then(data => {
                    selectedIndex = -1;
                    renderAdminSuggestions(data, dropdown, query);
                })
                .catch(err => {
                    if (err.name !== "AbortError") console.error("Admin search error:", err);
                });
            }, 250);
        });

        input.addEventListener("keydown", function (e) {
            const items = dropdown.querySelectorAll(".search-item");

            if (e.key === "ArrowDown") {
                if (!items.length) return;
                e.preventDefault();
                selectedIndex = (selectedIndex + 1) % items.length;
                updateAdminActiveItem(items, selectedIndex);
            } else if (e.key === "ArrowUp") {
                if (!items.length) return;
                e.preventDefault();
                selectedIndex = (selectedIndex - 1 + items.length) % items.length;
                updateAdminActiveItem(items, selectedIndex);
            } else if (e.key === "Enter" && selectedIndex >= 0 && items[selectedIndex]) {
                e.preventDefault();
                items[selectedIndex].click();
            } else if (e.key === "Escape") {
                dropdown.classList.remove("show");
            }
        });

        document.addEventListener("click", function (e) {
            if (!container.contains(e.target)) {
                dropdown.classList.remove("show");
            }
        });
    });
}

function updateAdminActiveItem(items, index) {
    items.forEach((item, i) => {
        if (i === index) {
            item.classList.add("active");
            item.scrollIntoView({ block: "nearest" });
        } else {
            item.classList.remove("active");
        }
    });
}

function highlightAdminMatch(text, query) {
    if (!text) return "";
    if (!query) return escapeHtml(text);
    const safeText = escapeHtml(text);
    const escapedQuery = query.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
    const regex = new RegExp(`(${escapedQuery})`, "gi");
    return safeText.replace(regex, `<mark class="tga-match-highlight">$1</mark>`);
}

function renderAdminSuggestions(data, dropdown, query) {
    if (!data || data.totalSuggestions === 0) {
        dropdown.innerHTML = `<div class="p-3 text-muted small text-center">No admin records found matching "${escapeHtml(query)}".</div>`;
        dropdown.classList.add("show");
        return;
    }

    let html = "";

    // Orders
    if (data.adminOrders && data.adminOrders.length) {
        html += `<div class="search-section-header text-uppercase text-xs fw-bold px-3 py-1 bg-light text-primary"><i class="bi bi-receipt me-1"></i> Orders (${data.adminOrders.length})</div>`;
        data.adminOrders.forEach(o => {
            html += `
                <a href="${o.url}" class="search-item d-flex align-items-center gap-2 px-3 py-2 text-decoration-none text-dark border-bottom">
                    <i class="bi bi-box-seam text-primary fs-5"></i>
                    <div class="flex-grow-1 overflow-hidden">
                        <div class="text-truncate small fw-semibold">${highlightAdminMatch(o.title, query)}</div>
                        <div class="text-muted text-xs">${escapeHtml(o.subtitle || '')}</div>
                    </div>
                    <span class="badge bg-primary-subtle text-primary">${escapeHtml(o.badge || '')}</span>
                </a>
            `;
        });
    }

    // Users
    if (data.adminUsers && data.adminUsers.length) {
        html += `<div class="search-section-header text-uppercase text-xs fw-bold px-3 py-1 bg-light text-secondary"><i class="bi bi-people me-1"></i> Users &amp; Patients (${data.adminUsers.length})</div>`;
        data.adminUsers.forEach(u => {
            html += `
                <a href="${u.url}" class="search-item d-flex align-items-center gap-2 px-3 py-2 text-decoration-none text-dark border-bottom">
                    <i class="bi bi-person text-secondary fs-5"></i>
                    <div class="flex-grow-1 overflow-hidden">
                        <div class="text-truncate small fw-semibold">${highlightAdminMatch(u.title, query)}</div>
                        <div class="text-muted text-xs">${escapeHtml(u.subtitle || '')}</div>
                    </div>
                    <span class="badge bg-secondary-subtle text-secondary">User</span>
                </a>
            `;
        });
    }

    // Products
    if (data.adminProducts && data.adminProducts.length) {
        html += `<div class="search-section-header text-uppercase text-xs fw-bold px-3 py-1 bg-light text-success"><i class="bi bi-boxes me-1"></i> Products &amp; Inventory (${data.adminProducts.length})</div>`;
        data.adminProducts.forEach(p => {
            html += `
                <a href="${p.url}" class="search-item d-flex align-items-center gap-2 px-3 py-2 text-decoration-none text-dark border-bottom">
                    <i class="bi bi-box text-success fs-5"></i>
                    <div class="flex-grow-1 overflow-hidden">
                        <div class="text-truncate small fw-semibold">${highlightAdminMatch(p.title, query)}</div>
                        <div class="text-muted text-xs">${escapeHtml(p.subtitle || '')}</div>
                    </div>
                    <span class="badge ${p.badge === 'In Stock' ? 'bg-success-subtle text-success' : 'bg-danger-subtle text-danger'}">${escapeHtml(p.badge || '')}</span>
                </a>
            `;
        });
    }

    // Doctors
    if (data.doctors && data.doctors.length) {
        html += `<div class="search-section-header text-uppercase text-xs fw-bold px-3 py-1 bg-light text-info"><i class="bi bi-person-check me-1"></i> Doctors (${data.doctors.length})</div>`;
        data.doctors.forEach(d => {
            html += `
                <a href="${d.url}" class="search-item d-flex align-items-center gap-2 px-3 py-2 text-decoration-none text-dark border-bottom">
                    <i class="bi bi-heart-pulse text-info fs-5"></i>
                    <div class="flex-grow-1 overflow-hidden">
                        <div class="text-truncate small fw-semibold">${highlightAdminMatch(d.title, query)}</div>
                        <div class="text-muted text-xs">${escapeHtml(d.subtitle || '')}</div>
                    </div>
                    <span class="badge bg-info-subtle text-info">${escapeHtml(d.badge || '')}</span>
                </a>
            `;
        });
    }

    html += `
        <div class="p-2 px-3 bg-light d-flex justify-content-between align-items-center">
            <span class="text-muted text-xs">Total ${data.totalSuggestions} records</span>
            <span class="text-muted text-xs">⚡ ${data.latencyMs}ms</span>
        </div>
    `;

    dropdown.innerHTML = html;
    dropdown.classList.add("show");
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

// --------------------------------------------------------------------------
// REUSABLE ACCESSIBLE CONFIRMATION & LOGOUT MODAL (PHASE 9)
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

    // Logout confirmation triggers
    const logoutTriggers = document.querySelectorAll("[data-tga-logout], .btn-logout-trigger");
    logoutTriggers.forEach(btn => {
        btn.addEventListener("click", function (e) {
            e.preventDefault();
            lastFocusedElement = btn;

            const actionUrl = btn.getAttribute("data-action-url") || "/admin-portal/auth/logout";

            if (modalTitleText) modalTitleText.innerText = "Confirm Administrative Sign Out";
            if (modalBody) modalBody.innerText = "Are you sure you want to terminate your administrative session?";
            if (proceedBtn) {
                proceedBtn.innerText = "Sign Out";
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

    // Generic destructive actions (e.g. reject doctor, delete record)
    const confirmTriggers = document.querySelectorAll("[data-confirm-action]");
    confirmTriggers.forEach(btn => {
        btn.addEventListener("click", function (e) {
            e.preventDefault();
            lastFocusedElement = btn;

            const title = btn.getAttribute("data-confirm-title") || "Confirm Administrative Action";
            const body = btn.getAttribute("data-confirm-body") || "Are you sure you want to proceed with this action?";
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
// SESSION INACTIVITY / IDLE TIMEOUT WARNING MODAL (PHASE 9)
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
                    window.location.href = "/Auth/Login";
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

            fetch("/Settings/Health", { method: "HEAD" }).catch(() => {});
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
// 4. DARK/LIGHT THEME SWITCHER
// --------------------------------------------------------------------------
function initThemeToggle() {
    const savedTheme = localStorage.getItem("tga-admin-theme") || "light";
    document.documentElement.setAttribute("data-theme", savedTheme);

    const toggleBtns = document.querySelectorAll(".theme-toggle-btn, #adminThemeToggle");
    toggleBtns.forEach(btn => {
        updateToggleBtnIcon(btn, savedTheme);
        btn.addEventListener("click", () => {
            const current = document.documentElement.getAttribute("data-theme") || "light";
            const next = current === "dark" ? "light" : "dark";
            document.documentElement.setAttribute("data-theme", next);
            localStorage.setItem("tga-admin-theme", next);
            toggleBtns.forEach(b => updateToggleBtnIcon(b, next));

            // Re-render charts with updated theme palette if Chart is present
            if (window.tgaCharts) {
                window.tgaCharts.forEach(c => {
                    if (c && typeof c.update === "function") c.update();
                });
            }
        });
    });
}

function updateToggleBtnIcon(btn, theme) {
    if (!btn) return;
    const icon = btn.querySelector("i");
    if (icon) {
        if (theme === "dark") {
            icon.className = "bi bi-sun-fill text-warning";
        } else {
            icon.className = "bi bi-moon-stars-fill text-muted";
        }
    }
}

// --------------------------------------------------------------------------
// 5. COLLAPSIBLE SIDEBAR WITH ICONS
// --------------------------------------------------------------------------
function initSidebarToggle() {
    const sidebar = document.querySelector("aside.admin-sidebar");
    const toggleBtns = document.querySelectorAll("#adminSidebarToggle, .btn-toggle-sidebar");
    if (!sidebar) return;

    const isCollapsed = localStorage.getItem("tga-sidebar-collapsed") === "true";
    if (isCollapsed) {
        sidebar.classList.add("collapsed");
    }

    toggleBtns.forEach(btn => {
        btn.addEventListener("click", () => {
            sidebar.classList.toggle("collapsed");
            const collapsed = sidebar.classList.contains("collapsed");
            localStorage.setItem("tga-sidebar-collapsed", collapsed);
        });
    });
}

// --------------------------------------------------------------------------
// 6. ANIMATED KPI COUNT-UP CARDS
// --------------------------------------------------------------------------
function initKpiCounterAnimation() {
    const kpiEls = document.querySelectorAll(".admin-kpi-number");
    if (!kpiEls.length) return;

    const prefersReducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

    kpiEls.forEach(el => {
        const target = parseFloat(el.getAttribute("data-target")) || 0;
        const prefix = el.getAttribute("data-prefix") || "";
        const suffix = el.getAttribute("data-suffix") || "";
        const decimals = parseInt(el.getAttribute("data-decimals"), 10) || 0;

        if (prefersReducedMotion) {
            el.textContent = prefix + (decimals > 0 ? target.toFixed(decimals) : target.toLocaleString("en-IN")) + suffix;
            return;
        }

        const duration = 1200;
        const start = performance.now();

        function step(now) {
            const progress = Math.min((now - start) / duration, 1);
            const ease = 1 - Math.pow(1 - progress, 3);
            const current = target * ease;
            el.textContent = prefix + (decimals > 0 ? current.toFixed(decimals) : Math.round(current).toLocaleString("en-IN")) + suffix;

            if (progress < 1) {
                requestAnimationFrame(step);
            } else {
                el.textContent = prefix + (decimals > 0 ? target.toFixed(decimals) : target.toLocaleString("en-IN")) + suffix;
            }
        }
        requestAnimationFrame(step);
    });
}

// --------------------------------------------------------------------------
// 7. CHART.JS EXECUTIVE DASHBOARD CHARTS (LOCAL BUNDLE ONLY)
// --------------------------------------------------------------------------
function initDashboardCharts() {
    window.tgaCharts = [];

    // Dismiss skeleton loaders if present
    const skeletonLoaders = document.querySelectorAll(".admin-skeleton-loading");
    if (skeletonLoaders.length) {
        setTimeout(() => {
            skeletonLoaders.forEach(s => s.classList.remove("active"));
        }, 300);
    }

    if (typeof Chart === "undefined") {
        console.warn("Chart.js not loaded. Skipping dashboard charts.");
        return;
    }

    // Chart.js global defaults
    Chart.defaults.font.family = '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
    Chart.defaults.color = "#6B7280";

    // 1. Sales Trend Line Chart
    const salesCanvas = document.getElementById("adminSalesTrendChart");
    if (salesCanvas) {
        const dates = JSON.parse(salesCanvas.getAttribute("data-dates") || "[]");
        const amounts = JSON.parse(salesCanvas.getAttribute("data-amounts") || "[]");

        const lineChart = new Chart(salesCanvas, {
            type: "line",
            data: {
                labels: dates,
                datasets: [{
                    label: "Daily Revenue (₹)",
                    data: amounts,
                    borderColor: "#1F5E3B",
                    backgroundColor: "rgba(31, 94, 59, 0.12)",
                    borderWidth: 2.5,
                    fill: true,
                    tension: 0.35,
                    pointBackgroundColor: "#E3A72F",
                    pointRadius: 4,
                    pointHoverRadius: 6
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: {
                            label: function (ctx) {
                                return "₹ " + Number(ctx.parsed.y).toLocaleString("en-IN");
                            }
                        }
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        grid: { color: "rgba(0, 0, 0, 0.05)" },
                        ticks: {
                            callback: function (val) {
                                return "₹" + Number(val).toLocaleString("en-IN");
                            }
                        }
                    },
                    x: {
                        grid: { display: false }
                    }
                }
            }
        });
        window.tgaCharts.push(lineChart);
    }

    // 2. Category Share Doughnut Chart
    const catCanvas = document.getElementById("adminCategoryShareChart");
    if (catCanvas) {
        const labels = JSON.parse(catCanvas.getAttribute("data-labels") || "[]");
        const counts = JSON.parse(catCanvas.getAttribute("data-counts") || "[]");

        const doughnutChart = new Chart(catCanvas, {
            type: "doughnut",
            data: {
                labels: labels,
                datasets: [{
                    data: counts,
                    backgroundColor: [
                        "#1F5E3B", "#2C7E50", "#E3A72F", "#F5C55A", "#A4553A", "#C46D50", "#3B82F6", "#8B5CF6"
                    ],
                    borderWidth: 2,
                    borderColor: "#FFFFFF"
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: "bottom",
                        labels: { boxWidth: 12, padding: 10, font: { size: 11 } }
                    }
                },
                cutout: "68%"
            }
        });
        window.tgaCharts.push(doughnutChart);
    }

    // 3. Top Products Bar Chart
    const prodCanvas = document.getElementById("adminTopProductsChart");
    if (prodCanvas) {
        const titles = JSON.parse(prodCanvas.getAttribute("data-titles") || "[]");
        const prices = JSON.parse(prodCanvas.getAttribute("data-prices") || "[]");

        const barChart = new Chart(prodCanvas, {
            type: "bar",
            data: {
                labels: titles,
                datasets: [{
                    label: "Unit Price (₹)",
                    data: prices,
                    backgroundColor: "rgba(44, 126, 80, 0.8)",
                    borderRadius: 6
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                indexAxis: "y",
                plugins: {
                    legend: { display: false }
                },
                scales: {
                    x: {
                        grid: { color: "rgba(0, 0, 0, 0.05)" },
                        ticks: {
                            callback: function (val) { return "₹" + val; }
                        }
                    },
                    y: {
                        grid: { display: false },
                        ticks: {
                            callback: function (val, idx) {
                                const full = titles[idx] || "";
                                return full.length > 20 ? full.slice(0, 18) + "…" : full;
                            }
                        }
                    }
                }
            }
        });
        window.tgaCharts.push(barChart);
    }
}

