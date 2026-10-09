// ==========================================================================
// THE GREAT AYURVEDA — CLIENT INTERACTION ENGINE
// Features: Smart Search, Prakriti Quiz, Deals Timer, Carousel, Ajax Cart
// ==========================================================================

document.addEventListener("DOMContentLoaded", function () {
    initSearchBox();
    initLogoutModal();
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

function renderSuggestions(data, dropdown) {
    if (!data || (!data.products?.length && !data.categories?.length && !data.doctors?.length)) {
        dropdown.innerHTML = `<div class="p-3 text-muted small text-center">No Ayurvedic formulations or Vaidyas found.</div>`;
        dropdown.classList.add("show");
        return;
    }

    let html = "";

    // Categories
    if (data.categories && data.categories.length) {
        html += `<div class="dropdown-header text-uppercase text-xs fw-bold px-3 pt-2 text-success">Categories</div>`;
        data.categories.forEach(cat => {
            html += `
                <a href="/Shop?categoryId=${cat.id}" class="search-item d-flex align-items-center gap-2 px-3 py-2 text-decoration-none text-dark">
                    <i class="bi bi-tag text-muted"></i>
                    <span>${escapeHtml(cat.name)}</span>
                </a>
            `;
        });
    }

    // Products
    if (data.products && data.products.length) {
        html += `<div class="dropdown-header text-uppercase text-xs fw-bold px-3 pt-2 text-success">Medicines &amp; Formulations</div>`;
        data.products.forEach(prod => {
            html += `
                <a href="/Shop/Details/${prod.id}" class="search-item d-flex align-items-center gap-2 px-3 py-2 text-decoration-none text-dark">
                    <img src="${prod.imageUrl || '/images/products/chyawanprash.svg'}" alt="" width="28" height="28" class="rounded object-fit-contain" />
                    <div class="flex-grow-1 overflow-hidden">
                        <div class="text-truncate small fw-semibold">${escapeHtml(prod.title)}</div>
                        <div class="text-muted text-xs">₹${prod.price} <span class="badge bg-light text-dark ms-1">${escapeHtml(prod.categoryName || '')}</span></div>
                    </div>
                </a>
            `;
        });
    }

    // Doctors
    if (data.doctors && data.doctors.length) {
        html += `<div class="dropdown-header text-uppercase text-xs fw-bold px-3 pt-2 text-success">Ayurvedic Doctors</div>`;
        data.doctors.forEach(doc => {
            html += `
                <a href="/Doctors/Profile/${doc.id}" class="search-item d-flex align-items-center gap-2 px-3 py-2 text-decoration-none text-dark">
                    <i class="bi bi-person-circle fs-5 text-success"></i>
                    <div class="flex-grow-1 overflow-hidden">
                        <div class="text-truncate small fw-semibold">Dr. ${escapeHtml(doc.doctorName)}</div>
                        <div class="text-muted text-xs">${escapeHtml(doc.specialization || '')} • ₹${doc.consultationFee}</div>
                    </div>
                </a>
            `;
        });
    }

    dropdown.innerHTML = html;
    dropdown.classList.add("show");
}

// --------------------------------------------------------------------------
// 2. LOGOUT CONFIRMATION MODAL TRIGGER
// --------------------------------------------------------------------------
function initLogoutModal() {
    const logoutTriggers = document.querySelectorAll("[data-tga-logout]");
    const modalEl = document.getElementById("confirmModal");
    if (!modalEl) return;

    logoutTriggers.forEach(btn => {
        btn.addEventListener("click", function (e) {
            e.preventDefault();
            const modalTitle = document.getElementById("confirmModalTitle");
            const modalBody = document.getElementById("confirmModalBody");
            const modalForm = document.getElementById("confirmModalForm");
            const actionUrl = btn.getAttribute("data-action-url") || "/Account/Logout";

            if (modalTitle) modalTitle.innerText = "Confirm Sign Out";
            if (modalBody) modalBody.innerText = "Are you sure you want to log out of your Ayurvedic patient account?";
            if (modalForm) {
                modalForm.setAttribute("action", actionUrl);
                modalForm.setAttribute("method", "post");
            }

            const modal = new bootstrap.Modal(modalEl);
            modal.show();
        });
    });
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
