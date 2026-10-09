import React, { useEffect, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { FaSearch, FaLeaf, FaSpa, FaUserMd } from "react-icons/fa";

const DISEASES_DATABASE = [
    {
        id: "digestive-acidity",
        name: "Acidity & GERD",
        ayurvedicName: "Amlapitta",
        category: "Digestive",
        dosha: "Pitta Aggravation",
        symptoms: ["Burning sensation in chest/throat", "Sour belching", "Nausea", "Indigestion"],
        causes: "Excessive spicy, fried, or sour foods, late-night dinners, high stress, caffeine overuse.",
        remedies: [
            "Fennel (Saunf) and Coriander seed water infused overnight",
            "1 tsp pure Amla powder with lukewarm water in the morning",
            "Fresh Aloe Vera juice (20ml) on an empty stomach",
            "Chewing 1 green cardamom pod after meals"
        ],
        herbs: ["Avipattikar Churna", "Yashtimadhu (Licorice)", "Amla", "Shatavari"],
        yoga: "Vajrasana (Post-meal), Sheetali Pranayama, Bhujangasana",
        dietTip: "Avoid hot spices, tomatoes, vinegar, and fermented food. Consume cooling foods like cucumber, sweet fruits, and coconut water."
    },
    {
        id: "digestive-ibs",
        name: "Indigestion & IBS",
        ayurvedicName: "Grahani Rog",
        category: "Digestive",
        dosha: "Vata-Kapha with Mandagni (Weak Digestive Fire)",
        symptoms: ["Bloating", "Irregular bowel movements", "Abdominal cramping", "Sluggish metabolism"],
        causes: "Incompatible food combinations (Viruddha Ahara), irregular meal timings, anxiety.",
        remedies: [
            "Fresh ginger slice with rock salt (Sendha Namak) 10 minutes before meals",
            "Buttermilk (Takra) churned with roasted cumin and mint",
            "Warm water sip-by-sip throughout the day",
            "Triphala tea before bedtime"
        ],
        herbs: ["Triphala Churna", "Hingwashtak Churna", "Musta", "Bilva (Bael)"],
        yoga: "Pavanamuktasana, Ardha Matsyendrasana, Agnisar Kriya",
        dietTip: "Eat freshly cooked warm meals. Avoid cold drinks, leftovers, and heavy raw salads."
    },
    {
        id: "joint-arthritis",
        name: "Joint Pain & Arthritis",
        ayurvedicName: "Sandhivata / Amavata",
        category: "Joint & Bone",
        dosha: "Vata vitiation with Ama (Toxin buildup)",
        symptoms: ["Joint stiffness in morning", "Swelling", "Crackling sounds (Crepitus)", "Reduced mobility"],
        causes: "Dry cold climate, aging, toxic accumulation from poor digestion, sedentary lifestyle.",
        remedies: [
            "Warm Sesame (Til) or Mahanarayan oil massage on joints daily",
            "Turmeric & ginger golden milk with a pinch of black pepper at bedtime",
            "Epsom salt or warm salt compress on affected joints",
            "Fenugreek (Methi) seeds soaked overnight swallowed with water"
        ],
        herbs: ["Shallaki (Boswellia)", "Guggulu (Yograj Guggul)", "Ashwagandha", "Rasna"],
        yoga: "Tadasana, Vrikshasana, Gentle Sukshma Vyayama, Setu Bandhasana",
        dietTip: "Favor warm, unctuous foods with ghee. Avoid stale food, nightshades (excess eggplant/tomatoes), and cold drafts."
    },
    {
        id: "respiratory-asthma",
        name: "Bronchial Asthma & Allergies",
        ayurvedicName: "Tamaka Shwasa",
        category: "Respiratory",
        dosha: "Kapha and Vata blocking Pranavaha Srotas",
        symptoms: ["Shortness of breath", "Wheezing sound", "Chest tightness", "Persistent dry or phlegmy cough"],
        causes: "Exposure to cold breeze, dust, pollen, damp climate, heavy mucus-forming diet.",
        remedies: [
            "Ginger juice mixed with 1 tsp raw honey and a pinch of black pepper",
            "Steam inhalation with 2-3 drops of Eucalyptus or Ajwain (Carom) seeds",
            "Tulsi (Holy Basil) leaf decoction with cinnamon and clove",
            "Mustard oil massage on chest followed by warm fomentation"
        ],
        herbs: ["Sitopaladi Churna", "Trikatu", "Vasaka (Adhatoda)", "Kantakari"],
        yoga: "Bhastrika Pranayama, Anulom Vilom, Matsyasana, Ustrasana",
        dietTip: "Strictly avoid refrigerated foods, curd at night, ice creams, and bananas in cold weather."
    },
    {
        id: "skin-eczema-acne",
        name: "Acne, Eczema & Psoriasis",
        ayurvedicName: "Kushtha / Yuvanpidika",
        category: "Skin & Hair",
        dosha: "Pitta and Rakta (Blood) toxicity",
        symptoms: ["Skin inflammation", "Red breakouts", "Itching", "Dry flaky patches or oily cysts"],
        causes: "Excessive deep-fried food, unhygienic conditions, chronic constipation, blood impurities.",
        remedies: [
            "Neem leaf water wash for cleansed skin",
            "Paste of Sandalwood (Chandan) and pure Turmeric applied topically",
            "Drinking warm water boiled with Manjistha root",
            "Pure Aloe Vera gel application on irritated skin"
        ],
        herbs: ["Neem", "Manjistha", "Khadirarishta", "Sarivadyasava"],
        yoga: "Sheetali & Sheetkari Pranayama, Surya Namaskar, Sarvangasana",
        dietTip: "Reduce refined sugar, spicy chutneys, and alcohol. Drink 2-3 liters of clean water and include bitter greens."
    },
    {
        id: "mental-stress-insomnia",
        name: "Stress, Anxiety & Insomnia",
        ayurvedicName: "Anidra / Chitta Udvega",
        category: "Mental Wellness",
        dosha: "Prana Vata and Sadhaka Pitta disturbance",
        symptoms: ["Restlessness", "Inability to fall or stay asleep", "Fatigue", "Racing thoughts", "Headaches"],
        causes: "Excess screen time at night, high pressure, suppression of natural urges, nutritional gaps.",
        remedies: [
            "Padabhyanga: Warm sesame or Brahmi oil foot massage for 5 mins before bed",
            "Warm nutmeg (Jaiphal) milk 30 minutes before sleeping",
            "Nasya: 2 drops of pure cow ghee in each nostril at bedtime",
            "Chamomile or Shankhpushpi calming herbal tea"
        ],
        herbs: ["Ashwagandha", "Brahmi", "Shankhpushpi", "Jatamansi"],
        yoga: "Shavasana, Yoga Nidra, Bhramari Pranayama, Balasana",
        dietTip: "Avoid evening caffeine. Eat warm light dinners at least 2.5 hours prior to sleep."
    },
    {
        id: "endocrine-diabetes",
        name: "High Blood Sugar / Pre-Diabetes",
        ayurvedicName: "Madhumeha",
        category: "Metabolic",
        dosha: "Kapha accumulation with Meda Dhatu (Fat tissue) imbalance",
        symptoms: ["Excessive thirst", "Frequent urination", "Unexplained fatigue", "Slow wound healing"],
        causes: "Sedentary routine, daytime sleeping, excessive consumption of refined carbohydrates and sweets.",
        remedies: [
            "Methi (Fenugreek) water: Soak 1 tbsp overnight, drink water and chew seeds in the morning",
            "Fresh Bitter Gourd (Karela) + Amla juice shots on empty stomach",
            "Cinnamon (Dalchini) powder (half tsp) boiled in water",
            "Jamun (Black plum) seed powder 1 tsp with water"
        ],
        herbs: ["Gudmar (Gymnema Sylvestre)", "Vijaysar", "Nisha Amalaki", "Chandraprabha Vati"],
        yoga: "Mandukasana, Dhanurasana, Kapalbhati Pranayama, Paschimottanasana",
        dietTip: "Replace white flour and white rice with Barley (Jau), Millets (Ragi/Jowar), and Moong dal."
    }
];

function DiseasesSearch() {
    const location = useLocation();
    const navigate = useNavigate();
    const initialQuery = location.state?.searchValue || "";

    const [searchTerm, setSearchTerm] = useState(initialQuery);
    const [selectedCategory, setSelectedCategory] = useState("All");

    useEffect(() => {
        if (location.state?.searchValue) {
            setSearchTerm(location.state.searchValue);
        }
    }, [location.state]);

    const categories = ["All", "Digestive", "Joint & Bone", "Respiratory", "Skin & Hair", "Mental Wellness", "Metabolic"];

    const filteredDiseases = DISEASES_DATABASE.filter((item) => {
        const matchesCategory = selectedCategory === "All" || item.category === selectedCategory;
        const q = searchTerm.toLowerCase();
        const matchesSearch =
            item.name.toLowerCase().includes(q) ||
            item.ayurvedicName.toLowerCase().includes(q) ||
            item.symptoms.some((s) => s.toLowerCase().includes(q)) ||
            item.dosha.toLowerCase().includes(q);

        return matchesCategory && matchesSearch;
    });

    return (
        <div className="container-fluid py-5 px-3 px-md-5">
            {/* Header */}
            <div className="text-center mb-5">
                <span className="badge bg-success-subtle text-success px-3 py-2 rounded-pill fw-semibold mb-2">
                    Ayurvedic Chikitsa Directory
                </span>
                <h1 className="display-6 fw-bold font-playfair text-dark mb-2">Ayurvedic Disease & Remedy Finder</h1>
                <p className="text-muted mx-auto" style={{ maxWidth: '700px' }}>
                    Discover authentic Ayurvedic root-cause explanations (Nidana), Dosha imbalances, time-tested home remedies, herbal formulations, and yoga therapy for common ailments.
                </p>

                {/* Search Bar */}
                <div className="mx-auto mt-4" style={{ maxWidth: '520px' }}>
                    <div className="input-group shadow-sm rounded-pill overflow-hidden border">
                        <span className="input-group-text bg-white border-0 ps-4 text-muted">
                            <FaSearch />
                        </span>
                        <input
                            type="text"
                            className="form-control border-0 py-3"
                            placeholder="Search disease, symptoms, or Sanskrit name (e.g. Acidity, Joint pain, Vata)..."
                            value={searchTerm}
                            onChange={(e) => setSearchTerm(e.target.value)}
                        />
                        {searchTerm && (
                            <button
                                className="btn btn-link text-muted pe-3 text-decoration-none"
                                onClick={() => setSearchTerm("")}
                            >
                                ✕
                            </button>
                        )}
                    </div>
                </div>

                {/* Category Chips */}
                <div className="d-flex flex-wrap justify-content-center gap-2 mt-4">
                    {categories.map((cat) => (
                        <button
                            key={cat}
                            onClick={() => setSelectedCategory(cat)}
                            className={`btn btn-sm rounded-pill px-3 py-2 fw-semibold transition-all ${
                                selectedCategory === cat
                                    ? 'btn-success text-white shadow-sm'
                                    : 'btn-outline-secondary'
                            }`}
                        >
                            {cat}
                        </button>
                    ))}
                </div>
            </div>

            {/* Results Grid */}
            {filteredDiseases.length === 0 ? (
                <div className="text-center py-5 card border-0 shadow-sm rounded-4 p-5 mx-auto" style={{ maxWidth: '500px' }}>
                    <FaLeaf size={40} className="text-muted mx-auto mb-3" />
                    <h5 className="fw-semibold text-secondary">No matching health conditions found</h5>
                    <p className="text-muted small">Try searching with a different symptom or disease name, or explore all categories.</p>
                    <button
                        className="btn btn-outline-success rounded-pill mx-auto px-4 mt-2"
                        onClick={() => { setSearchTerm(""); setSelectedCategory("All"); }}
                    >
                        Reset Filters
                    </button>
                </div>
            ) : (
                <div className="row g-4">
                    {filteredDiseases.map((item) => (
                        <div className="col-12 col-lg-6" key={item.id}>
                            <div className="card h-100 border-0 shadow-sm rounded-4 p-4 card-ayur transition-all">
                                <div className="d-flex justify-content-between align-items-start mb-3 border-bottom pb-3">
                                    <div>
                                        <span className="badge bg-success-subtle text-success rounded-pill px-3 py-1 fw-semibold small mb-1">
                                            {item.category}
                                        </span>
                                        <h4 className="fw-bold text-dark font-playfair mb-0">{item.name}</h4>
                                        <span className="text-muted small fst-italic">Ayurvedic Name: <strong>{item.ayurvedicName}</strong></span>
                                    </div>
                                    <span className="badge bg-warning-subtle text-dark border px-3 py-2 rounded-pill small fw-semibold">
                                        {item.dosha}
                                    </span>
                                </div>

                                <div className="mb-3">
                                    <strong className="text-dark small d-block mb-1">Common Symptoms:</strong>
                                    <div className="d-flex flex-wrap gap-1">
                                        {item.symptoms.map((sym, sIdx) => (
                                            <span key={sIdx} className="badge bg-light text-secondary border small">
                                                {sym}
                                            </span>
                                        ))}
                                    </div>
                                </div>

                                <div className="mb-3">
                                    <strong className="text-success small d-flex align-items-center gap-1 mb-2">
                                        <FaLeaf /> Natural Home Remedies (Gharelu Nuskhe):
                                    </strong>
                                    <ul className="list-unstyled mb-0 small ps-2 space-y-1">
                                        {item.remedies.map((rem, rIdx) => (
                                            <li key={rIdx} className="mb-1 text-secondary">
                                                • {rem}
                                            </li>
                                        ))}
                                    </ul>
                                </div>

                                <div className="row g-2 mb-3 bg-light p-3 rounded-3 small">
                                    <div className="col-12 col-md-6">
                                        <span className="fw-bold text-dark d-block">Recommended Herbs:</span>
                                        <span className="text-muted">{item.herbs.join(", ")}</span>
                                    </div>
                                    <div className="col-12 col-md-6">
                                        <span className="fw-bold text-dark d-block">Therapeutic Yoga:</span>
                                        <span className="text-muted">{item.yoga}</span>
                                    </div>
                                </div>

                                <div className="d-flex justify-content-between align-items-center pt-2 mt-auto">
                                    <button
                                        onClick={() => navigate('/yoga')}
                                        className="btn btn-sm btn-outline-success rounded-pill px-3 d-flex align-items-center gap-1"
                                    >
                                        <FaSpa /> Explore Yoga
                                    </button>
                                    <button
                                        onClick={() => navigate('/doctorconsult')}
                                        className="btn btn-sm btn-primary-ayur text-white rounded-pill px-3 d-flex align-items-center gap-1"
                                    >
                                        <FaUserMd /> Consult Vaidya
                                    </button>
                                </div>
                            </div>
                        </div>
                    ))}
                </div>
            )}
            {/* Responsible Ayurveda Medical Disclaimer */}
            <div className="alert alert-secondary mt-5 rounded-4 p-4 border-0 shadow-sm text-center">
                <h6 className="fw-bold text-dark mb-2">⚖️ Responsible Ayurveda & Health Advisory</h6>
                <p className="small text-muted mb-0 mx-auto" style={{ maxWidth: '850px' }}>
                    The information provided above is for educational, traditional, and preventative wellness purposes only and does not constitute formal medical diagnosis or advice. Do not stop or alter any ongoing physician-prescribed treatments without consulting your primary doctor. Individuals with chronic illnesses, pregnant or breastfeeding mothers, and elderly patients must seek personalized guidance from a certified BAMS Ayurvedic practitioner before starting any intensive herbal remedies.
                </p>
            </div>
        </div>
    );
}

export default DiseasesSearch;