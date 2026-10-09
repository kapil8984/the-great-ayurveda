// The Great Ayurveda - Client-side core scripts
document.addEventListener("DOMContentLoaded", function () {
    initSearchBox();
    initLogoutModal();
    initAjaxCart();
});

// Smart Search with 150ms debounce and keyboard navigation
function initSearchBox() {
    const searchInputs = document.querySelectorAll(".tga-search-input");
    
    searchInputs.forEach(input => {
        const container = input.closest(".search-container");
        if (!container) return;
        
        const dropdown = container.querySelector(".search-results-dropdown");
        if (!dropdown) return;

        let debounceTimer;
        let selectedIndex = -1;

        input.addEventListener("input", function () {
            clearTimeout(debounceTimer);
            const query = input.value.trim();
            if (query.length < 2) {
                dropdown.innerHTML = "";
                dropdown.classList.remove("show");
                return;
            }

            debounceTimer = setTimeout(() => {
                fetch(`/search/suggest?q=${encodeURIComponent(query)}`)
                    .then(res => res.json())
                    .then(data => {
                        renderSuggestions(data, dropdown);
                    })
                    .catch(err => console.error("Search suggest error:", err));
            }, 150);
        });

        input.addEventListener("keydown", function (e) {
            const items = dropdown.querySelectorAll(".search-item");
            if (!items.length) return;

            if (e.key === "ArrowDown") {
                e.preventDefault();
                selectedIndex = (selectedIndex + 1) % items.length;
                updateActiveItem(items, selectedIndex);
            } else if (e.key === "ArrowUp") {
                e.preventDefault();
                selectedIndex = (selectedIndex - 1 + items.length) % items.length;
                updateActiveItem(items, selectedIndex);
            } else if (e.key === "Enter" && selectedIndex >= 0) {
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

function updateActiveItem(items, activeIndex) {
    items.forEach((item, idx) => {
        if (idx === activeIndex) {
            item.classList.add("active");
            item.scrollIntoView({ block: "nearest" });
        } else {
            item.classList.remove("active");
        }
    });
}

function renderSuggestions(data, dropdown) {
    dropdown.innerHTML = "";
    let hasResults = false;

    // Products
    if (data.products && data.products.length > 0) {
        hasResults = true;
        const section = document.createElement("div");
        section.innerHTML = `<div class="search-section-header">Medicines & Products</div>`;
        data.products.forEach(p => {
            const item = document.createElement("a");
            item.className = "search-item";
            item.href = p.url;
            item.innerHTML = `
                <div class="flex-grow-1">
                    <div class="fw-semibold">${escapeHtml(p.title)}</div>
                    <div class="small text-muted">${escapeHtml(p.subtitle || "")}</div>
                </div>
                ${p.badge ? `<span class="badge bg-success-subtle text-success">${escapeHtml(p.badge)}</span>` : ""}
            `;
            section.appendChild(item);
        });
        dropdown.appendChild(section);
    }

    // Doctors
    if (data.doctors && data.doctors.length > 0) {
        hasResults = true;
        const section = document.createElement("div");
        section.innerHTML = `<div class="search-section-header">Ayurvedic Doctors</div>`;
        data.doctors.forEach(d => {
            const item = document.createElement("a");
            item.className = "search-item";
            item.href = d.url;
            item.innerHTML = `
                <div class="flex-grow-1">
                    <div class="fw-semibold">${escapeHtml(d.title)}</div>
                    <div class="small text-muted">${escapeHtml(d.subtitle || "")}</div>
                </div>
                <span class="badge bg-primary-subtle text-primary">${escapeHtml(d.badge || "Doctor")}</span>
            `;
            section.appendChild(item);
        });
        dropdown.appendChild(section);
    }

    // Categories
    if (data.categories && data.categories.length > 0) {
        hasResults = true;
        const section = document.createElement("div");
        section.innerHTML = `<div class="search-section-header">Categories</div>`;
        data.categories.forEach(c => {
            const item = document.createElement("a");
            item.className = "search-item";
            item.href = c.url;
            item.innerHTML = `<div class="fw-semibold">${escapeHtml(c.title)}</div>`;
            section.appendChild(item);
        });
        dropdown.appendChild(section);
    }

    // Health Concerns
    if (data.concerns && data.concerns.length > 0) {
        hasResults = true;
        const section = document.createElement("div");
        section.innerHTML = `<div class="search-section-header">Health Concerns & Remedies</div>`;
        data.concerns.forEach(c => {
            const item = document.createElement("a");
            item.className = "search-item";
            item.href = c.url;
            item.innerHTML = `
                <div class="flex-grow-1">
                    <div class="fw-semibold">${escapeHtml(c.title)}</div>
                    <div class="small text-muted">${escapeHtml(c.subtitle || "")}</div>
                </div>
            `;
            section.appendChild(item);
        });
        dropdown.appendChild(section);
    }

    if (hasResults) {
        dropdown.classList.add("show");
    } else {
        dropdown.innerHTML = `<div class="p-3 text-center text-muted small">No matches found. Try searching for "Triphala", "Ashwagandha", "Digestion", or a doctor's name.</div>`;
        dropdown.classList.add("show");
    }
}

// Reusable ConfirmModal for Logout and destructive actions
function initLogoutModal() {
    const logoutTriggers = document.querySelectorAll(".btn-logout-trigger");
    const confirmModalEl = document.getElementById("confirmActionModal");
    
    if (!confirmModalEl) return;

    logoutTriggers.forEach(btn => {
        btn.addEventListener("click", function (e) {
            e.preventDefault();
            const form = btn.closest("form") || document.getElementById("logoutForm");
            
            document.getElementById("confirmModalTitle").innerText = "Confirm Sign Out";
            document.getElementById("confirmModalBody").innerText = "Are you sure you want to sign out of your account?";
            const proceedBtn = document.getElementById("confirmModalProceedBtn");
            
            proceedBtn.onclick = function () {
                if (form) form.submit();
            };

            const modal = new bootstrap.Modal(confirmModalEl);
            modal.show();
        });
    });
}

// AJAX Cart Quick-Add
function initAjaxCart() {
    document.querySelectorAll(".ajax-add-to-cart").forEach(btn => {
        btn.addEventListener("click", function (e) {
            e.preventDefault();
            const productId = btn.getAttribute("data-product-id");
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

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
                if (data.success) {
                    const badge = document.getElementById("cartCountBadge");
                    if (badge) badge.innerText = data.cartCount;
                    showToast("Added to Cart", data.message || "Item added to your basket.");
                } else {
                    showToast("Notice", data.message || "Unable to add item.", "warning");
                }
            })
            .catch(err => {
                console.error("Cart error:", err);
            });
        });
    });
}

function showToast(title, message, type = "success") {
    const toastContainer = document.getElementById("tgaToastContainer");
    if (!toastContainer) return;

    const toastEl = document.createElement("div");
    toastEl.className = `toast align-items-center text-bg-${type === "warning" ? "warning" : "dark"} border-0`;
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
