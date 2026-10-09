import React from 'react';
import { useNavigate } from 'react-router-dom';
import { 
  FaHeartbeat, FaBalanceScale, FaSeedling, 
  FaUserMd, FaShieldAlt, FaBookOpen, FaArrowRight 
} from 'react-icons/fa';

function About() {
  const navigate = useNavigate();

  return (
    <div className="bg-white">
      {/* Hero Banner */}
      <section 
        className="py-5 text-white" 
        style={{ 
          background: 'linear-gradient(135deg, #0f2c1d 0%, #1b4332 50%, #2d6a4f 100%)',
          minHeight: '340px',
          display: 'flex',
          alignItems: 'center'
        }}
      >
        <div className="container py-4 text-center">
          <span className="badge badge-gold px-3 py-2 rounded-pill fw-bold mb-3 d-inline-flex align-items-center gap-2">
            <FaBookOpen /> 5,000 Years of Timeless Vedic Science
          </span>
          <h1 className="display-4 fw-bold font-playfair text-white mb-3">
            The Living Philosophy of Ayurveda
          </h1>
          <p className="lead mx-auto text-light opacity-90" style={{ maxWidth: '720px' }}>
            Ayurveda—literally translated from Sanskrit as "The Science of Life" (Āyus = life, Veda = knowledge)—is humanity's oldest systematized holistic medicine and conscious lifestyle framework.
          </p>
        </div>
      </section>

      {/* Core Mission & Heritage */}
      <section className="py-5">
        <div className="container">
          <div className="row g-5 align-items-center">
            <div className="col-12 col-lg-6">
              <span className="badge badge-ayur mb-2">Our Heritage & Calling</span>
              <h2 className="display-6 fw-bold font-playfair mb-3" style={{ color: '#0f2c1d' }}>
                Restoring Sacred Healing to the Contemporary World
              </h2>
              <p className="text-muted mb-3">
                Founded upon the classical treatises of <em>Charaka Samhita</em>, <em>Sushruta Samhita</em>, and <em>Ashtanga Hridaya</em>, <strong>The Great Ayurveda</strong> was established to bridge ancient Vedic wisdom with modern analytical purity.
              </p>
              <p className="text-muted mb-4">
                We reject diluted commercial remedies and artificial additives. Every formulation in our apothecary adheres strictly to classical Ayurvedic Pharmacopoeia protocols, using ethically wildcrafted and organic botanical harvests processed in GMP-certified herbal facilities.
              </p>

              <div className="d-flex flex-wrap gap-3">
                <button onClick={() => navigate('/get-all-products')} className="btn-ayur-primary">
                  Explore Remedies <FaArrowRight size={13} />
                </button>
                <button onClick={() => navigate('/doctorconsult')} className="btn-ayur-outline">
                  Meet Our Vaidyas
                </button>
              </div>
            </div>

            <div className="col-12 col-lg-6">
              <div className="position-relative">
                <img
                  src="https://images.unsplash.com/photo-1544367567-0f2fcb009e0b?auto=format&fit=crop&w=1000&q=80"
                  alt="Ayurvedic herbs and meditation"
                  className="img-fluid rounded-4 shadow-lg object-fit-cover w-100"
                  style={{ maxHeight: '420px' }}
                />
                <div 
                  className="position-absolute bottom-0 start-0 m-4 p-3 bg-white bg-opacity-95 rounded-3 shadow-sm border border-light"
                  style={{ maxWidth: '280px' }}
                >
                  <small className="text-success fw-bold d-block">Pure Botanical Alchemy</small>
                  <span className="small text-muted">100% Non-GMO, Heavy-Metal Tested, and Classical Decoctions</span>
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* The 5 Mahabhutas & 3 Doshas */}
      <section className="py-5 bg-light border-top border-bottom">
        <div className="container">
          <div className="text-center mb-5">
            <span className="badge badge-ayur mb-2">Vedic Cosmogony</span>
            <h2 className="display-6 fw-bold font-playfair" style={{ color: '#0f2c1d' }}>
              Pancha Mahabhuta: The Five Universal Elements
            </h2>
            <p className="text-muted mx-auto" style={{ maxWidth: '640px' }}>
              Everything in nature and the human physiology is constituted of five fundamental elements, manifesting as the three vital Doshas.
            </p>
          </div>

          <div className="row g-4">
            <div className="col-12 col-md-4">
              <div className="card h-100 border-0 shadow-sm rounded-4 p-4 text-start bg-white">
                <div className="d-flex align-items-center justify-content-between mb-3">
                  <h4 className="fw-bold font-playfair mb-0 text-dark">Vāta (वात)</h4>
                  <span className="badge bg-primary-subtle text-primary rounded-pill">Space & Air</span>
                </div>
                <p className="text-muted small mb-3">
                  Governs motion, nerve conduction, respiration, and cellular division. When harmonious, Vata fuels visionary thinking and mental agility.
                </p>
                <div className="mt-auto pt-3 border-top">
                  <small className="text-success fw-bold d-block">Signs of Imbalance:</small>
                  <span className="small text-muted">Dry skin, constipation, anxiety, restless sleep, and joint crepitus.</span>
                </div>
              </div>
            </div>

            <div className="col-12 col-md-4">
              <div className="card h-100 border-0 shadow-sm rounded-4 p-4 text-start bg-white">
                <div className="d-flex align-items-center justify-content-between mb-3">
                  <h4 className="fw-bold font-playfair mb-0 text-dark">Pitta (पित्त)</h4>
                  <span className="badge bg-danger-subtle text-danger rounded-pill">Fire & Water</span>
                </div>
                <p className="text-muted small mb-3">
                  Governs digestion (Agni), thermoregulation, enzyme synthesis, and metabolic transformation. Balanced Pitta bestows luminous focus and courage.
                </p>
                <div className="mt-auto pt-3 border-top">
                  <small className="text-success fw-bold d-block">Signs of Imbalance:</small>
                  <span className="small text-muted">Acid reflux, skin eruptions, inflammatory conditions, and irritability.</span>
                </div>
              </div>
            </div>

            <div className="col-12 col-md-4">
              <div className="card h-100 border-0 shadow-sm rounded-4 p-4 text-start bg-white">
                <div className="d-flex align-items-center justify-content-between mb-3">
                  <h4 className="fw-bold font-playfair mb-0 text-dark">Kapha (कफ)</h4>
                  <span className="badge bg-success-subtle text-success rounded-pill">Water & Earth</span>
                </div>
                <p className="text-muted small mb-3">
                  Governs biological structure, joint lubrication, mucosal immunity, and emotional patience. Balanced Kapha yields endurance and calm serenity.
                </p>
                <div className="mt-auto pt-3 border-top">
                  <small className="text-success fw-bold d-block">Signs of Imbalance:</small>
                  <span className="small text-muted">Metabolic lethargy, congestion, weight accumulation, and mental heaviness.</span>
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* The 4 Pillars of Classical Treatment */}
      <section className="py-5">
        <div className="container">
          <div className="text-center mb-5">
            <span className="badge badge-gold mb-2">Chikitsa Chatushpada</span>
            <h2 className="display-6 fw-bold font-playfair" style={{ color: '#0f2c1d' }}>
              The Four Pillars of Successful Healing
            </h2>
            <p className="text-muted mx-auto" style={{ maxWidth: '650px' }}>
              Charaka Samhita states that sustainable cure requires the harmonious coordination of four foundational elements.
            </p>
          </div>

          <div className="row g-4 text-center">
            <div className="col-6 col-lg-3">
              <div className="p-4 rounded-4 bg-white border h-100 shadow-sm">
                <div className="rounded-circle bg-success-subtle text-success p-3 d-inline-flex mb-3">
                  <FaUserMd size={28} />
                </div>
                <h5 className="fw-bold font-playfair text-dark">1. Bhishag (The Physician)</h5>
                <p className="text-muted small mb-0">Certified, compassionate Vaidyas with deep diagnostic acumen (Nadi Pariksha).</p>
              </div>
            </div>

            <div className="col-6 col-lg-3">
              <div className="p-4 rounded-4 bg-white border h-100 shadow-sm">
                <div className="rounded-circle bg-success-subtle text-success p-3 d-inline-flex mb-3">
                  <FaSeedling size={28} />
                </div>
                <h5 className="fw-bold font-playfair text-dark">2. Dravya (The Medicine)</h5>
                <p className="text-muted small mb-0">Unadulterated, potent classical herbs prepared according to strict Shastric canons.</p>
              </div>
            </div>

            <div className="col-6 col-lg-3">
              <div className="p-4 rounded-4 bg-white border h-100 shadow-sm">
                <div className="rounded-circle bg-success-subtle text-success p-3 d-inline-flex mb-3">
                  <FaHeartbeat size={28} />
                </div>
                <h5 className="fw-bold font-playfair text-dark">3. Upasthata (The Caregiver)</h5>
                <p className="text-muted small mb-0">Supportive family and holistic therapists guiding panchakarma therapies.</p>
              </div>
            </div>

            <div className="col-6 col-lg-3">
              <div className="p-4 rounded-4 bg-white border h-100 shadow-sm">
                <div className="rounded-circle bg-success-subtle text-success p-3 d-inline-flex mb-3">
                  <FaBalanceScale size={28} />
                </div>
                <h5 className="fw-bold font-playfair text-dark">4. Rogi (The Patient)</h5>
                <p className="text-muted small mb-0">An empowered participant dedicated to mindful diet, dinacharya, and recovery.</p>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* Responsible Medical Disclaimer */}
      <section className="py-4 bg-light">
        <div className="container">
          <div className="medical-disclaimer-box">
            <h6 className="fw-bold text-dark d-flex align-items-center gap-2 mb-2">
              <FaShieldAlt className="text-warning" /> Responsible Ayurveda & Medical Guidance Notice
            </h6>
            <p className="mb-2">
              The products, classical texts, and educational articles shared on <strong>The Great Ayurveda</strong> are intended for general health preservation, rejuvenation (Rasayana), and educational exploration based on recognized Ayurvedic traditions. They are not intended to substitute for emergency medical care or diagnosis.
            </p>
            <p className="mb-0">
              Never discontinue physician-prescribed allopathic medicines without consulting a qualified medical doctor. If you are pregnant, nursing, taking anticoagulant therapies, or managing acute chronic illnesses, always consult with a licensed healthcare practitioner or certified Ayurvedic Vaidya before introducing herbal formulations.
            </p>
          </div>
        </div>
      </section>
    </div>
  );
}

export default About;
