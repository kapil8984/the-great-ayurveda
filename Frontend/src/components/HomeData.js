import React, { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import Api, { apiClient } from "./WebApi/Api";
import { toast } from "react-toastify";
import starGenerate from "./product/GenerateRating";
import Carousel from "./Carousel";
import { 
  FaLeaf, FaUserMd, FaSpa, FaShieldAlt, 
  FaShoppingBag, FaArrowRight, FaMortarPestle, 
  FaFire, FaWind, FaWater, 
  FaEnvelope, FaPaperPlane
} from "react-icons/fa";

function HomeData() {
  const navigate = useNavigate();
  const [categories, setCategories] = useState([]);
  const [featuredProducts, setFeaturedProducts] = useState([]);
  const [activeDoctors, setActiveDoctors] = useState([]);
  const [yogaList, setYogaList] = useState([]);
  const [, setLoadingData] = useState(true);
  const [newsletterEmail, setNewsletterEmail] = useState('');
  const [newsletterSubscribed, setNewsletterSubscribed] = useState(false);

  const currentUser = JSON.parse(sessionStorage.getItem('user-details') || 'null');

  useEffect(() => {
    loadHomeData();
  }, []);

  const loadHomeData = async () => {
    try {
      setLoadingData(true);
      const [catRes, prodRes, docRes, yogaRes] = await Promise.allSettled([
        apiClient.get(Api.getCategory),
        apiClient.get(Api.getAllProduct),
        apiClient.get(Api.getActiveDoctors),
        apiClient.get(Api.getAllYoga)
      ]);

      if (catRes.status === "fulfilled") {
        const cats = catRes.value.data.categories || catRes.value.data.category || [];
        setCategories(cats.slice(0, 6));
      }

      if (prodRes.status === "fulfilled") {
        const prods = prodRes.value.data.product || prodRes.value.data.products || [];
        setFeaturedProducts(prods.slice(0, 4));
      }

      if (docRes.status === "fulfilled") {
        const docs = docRes.value.data.doctors || docRes.value.data.doctorList || docRes.value.data || [];
        setActiveDoctors(Array.isArray(docs) ? docs.slice(0, 3) : []);
      }

      if (yogaRes.status === "fulfilled") {
        const yogas = yogaRes.value.data.Yogas || yogaRes.value.data.yogas || [];
        setYogaList(yogas.slice(0, 3));
      }
    } catch (err) {
      console.error("Home data fetch error:", err);
    } finally {
      setLoadingData(false);
    }
  };

  const handleAddToCart = async (product) => {
    if (!currentUser) {
      toast.info("Please sign in to add remedies to your cart");
      navigate('/user-account');
      return;
    }

    if (product.stock <= 0) {
      toast.warning("This formulation is currently out of stock");
      return;
    }

    try {
      const prodId = product._id || product.id;
      const userId = currentUser._id || currentUser.id;
      const res = await apiClient.post(Api.addToCart, { productId: prodId, userId, quantity: 1 });
      toast.success(res.data.message || `${product.title} added to cart!`);
      // Trigger header reload if needed
      window.dispatchEvent(new Event('storage'));
    } catch (err) {
      console.error(err);
      toast.error("Could not add item to cart");
    }
  };

  const handleNewsletterSubmit = (e) => {
    e.preventDefault();
    if (!newsletterEmail || !newsletterEmail.includes('@')) {
      toast.warning("Please enter a valid email address");
      return;
    }
    setNewsletterSubscribed(true);
    toast.success("Namaste! You are now subscribed to sacred seasonal health dispatches.");
    setNewsletterEmail('');
  };

  return (
    <div className="home-container">
      {/* 1. Hero Carousel */}
      <Carousel />

      {/* 2. Trust & Quality Assurance Strip */}
      <div className="bg-white py-4 border-bottom shadow-xs">
        <div className="container">
          <div className="row g-4 text-center">
            <div className="col-6 col-md-3">
              <div className="d-flex align-items-center justify-content-center gap-3">
                <div className="rounded-circle bg-success-subtle text-success p-3">
                  <FaLeaf size={22} />
                </div>
                <div className="text-start">
                  <h6 className="fw-bold mb-0 text-dark">100% Classical Herbs</h6>
                  <small className="text-muted">Pure Botanical Ingredients</small>
                </div>
              </div>
            </div>

            <div className="col-6 col-md-3">
              <div className="d-flex align-items-center justify-content-center gap-3">
                <div className="rounded-circle bg-success-subtle text-success p-3">
                  <FaUserMd size={22} />
                </div>
                <div className="text-start">
                  <h6 className="fw-bold mb-0 text-dark">Certified Vaidyas</h6>
                  <small className="text-muted">BAMS & MD Practitioners</small>
                </div>
              </div>
            </div>

            <div className="col-6 col-md-3">
              <div className="d-flex align-items-center justify-content-center gap-3">
                <div className="rounded-circle bg-success-subtle text-success p-3">
                  <FaSpa size={22} />
                </div>
                <div className="text-start">
                  <h6 className="fw-bold mb-0 text-dark">Therapeutic Yoga</h6>
                  <small className="text-muted">Guided Asanas for Recovery</small>
                </div>
              </div>
            </div>

            <div className="col-6 col-md-3">
              <div className="d-flex align-items-center justify-content-center gap-3">
                <div className="rounded-circle bg-success-subtle text-success p-3">
                  <FaShieldAlt size={22} />
                </div>
                <div className="text-start">
                  <h6 className="fw-bold mb-0 text-dark">GMP Quality Certified</h6>
                  <small className="text-muted">Heavy Metal & Toxin Tested</small>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* 3. Ayurvedic Philosophy: The 3 Doshas (Tridosha Principle) */}
      <section className="py-5 bg-light">
        <div className="container text-center">
          <span className="badge badge-ayur mb-2">Vedic Cosmogony</span>
          <h2 className="display-6 fw-bold font-playfair mb-2" style={{ color: '#0f2c1d' }}>
            The Tridosha Principle of Vital Equilibrium
          </h2>
          <p className="text-muted mx-auto mb-5" style={{ maxWidth: '680px' }}>
            Ayurveda teaches that physical health and mental clarity depend upon dynamic harmony between three biological energies: Vāta, Pitta, and Kapha.
          </p>

          <div className="row g-4 text-start">
            {/* Vata */}
            <div className="col-12 col-md-4">
              <div className="dosha-card vata">
                <div className="d-flex align-items-center justify-content-between mb-3">
                  <div>
                    <h4 className="fw-bold font-playfair mb-0 text-dark">Vāta (वात)</h4>
                    <small className="text-muted">Space & Air (Akasha & Vayu)</small>
                  </div>
                  <span className="rounded-circle bg-primary-subtle text-primary p-2">
                    <FaWind size={20} />
                  </span>
                </div>
                <p className="text-muted small mb-3">
                  Governs all biological motion, nerve transmissions, respiration, and cellular division. Balanced Vata yields inspired creativity, mental agility, and enthusiasm.
                </p>
                <div className="mt-auto pt-3 border-top">
                  <small className="text-primary fw-bold d-block">Recommended Therapies:</small>
                  <small className="text-muted d-block">Warm oily meals, Sesame Oil Abhyanga massage, and Ashwagandha.</small>
                </div>
              </div>
            </div>

            {/* Pitta */}
            <div className="col-12 col-md-4">
              <div className="dosha-card pitta">
                <div className="d-flex align-items-center justify-content-between mb-3">
                  <div>
                    <h4 className="fw-bold font-playfair mb-0 text-dark">Pitta (पित्त)</h4>
                    <small className="text-muted">Fire & Water (Tejas & Jala)</small>
                  </div>
                  <span className="rounded-circle bg-danger-subtle text-danger p-2">
                    <FaFire size={20} />
                  </span>
                </div>
                <p className="text-muted small mb-3">
                  Governs metabolic transformation, digestion (Agni), thermoregulation, and intellect. Balanced Pitta brings luminous skin, clear intellect, and courage.
                </p>
                <div className="mt-auto pt-3 border-top">
                  <small className="text-danger fw-bold d-block">Recommended Therapies:</small>
                  <small className="text-muted d-block">Cooling diet, Amalaki Rasayana, Shatavari, and Coconut water hydration.</small>
                </div>
              </div>
            </div>

            {/* Kapha */}
            <div className="col-12 col-md-4">
              <div className="dosha-card kapha">
                <div className="d-flex align-items-center justify-content-between mb-3">
                  <div>
                    <h4 className="fw-bold font-playfair mb-0 text-dark">Kapha (कफ)</h4>
                    <small className="text-muted">Earth & Water (Prithvi & Jala)</small>
                  </div>
                  <span className="rounded-circle bg-success-subtle text-success p-2">
                    <FaWater size={20} />
                  </span>
                </div>
                <p className="text-muted small mb-3">
                  Governs musculoskeletal structure, mucosal immunity, stamina, and biological cohesion. Balanced Kapha bestows enduring strength and calm compassion.
                </p>
                <div className="mt-auto pt-3 border-top">
                  <small className="text-success fw-bold d-block">Recommended Therapies:</small>
                  <small className="text-muted d-block">Warming pungent spices, Trikatu, Dry Garshana massage, and invigorating Surya Namaskar.</small>
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* 4. Ayurvedic Herb Categories */}
      {categories.length > 0 && (
        <section className="py-5 bg-white">
          <div className="container">
            <div className="d-flex justify-content-between align-items-end mb-4">
              <div>
                <span className="badge badge-ayur mb-2">Herbal Taxonomy</span>
                <h2 className="display-6 fw-bold font-playfair mb-1" style={{ color: '#0f2c1d' }}>
                  Explore Botanical Formulations
                </h2>
                <p className="text-muted mb-0 small">Classical Shastric categories organized for targeted physiological support</p>
              </div>
              <button
                onClick={() => navigate('/get-all-products')}
                className="btn btn-outline-success rounded-pill px-4 btn-sm d-none d-md-flex align-items-center gap-2 fw-semibold"
              >
                Browse All Formulations <FaArrowRight size={12} />
              </button>
            </div>

            <div className="row g-3">
              {categories.map((cat, idx) => (
                <div className="col-6 col-md-4 col-lg-2" key={idx}>
                  <div
                    onClick={() => navigate('/get-all-products', { state: { categoryId: cat._id || cat.id, categoryName: cat.name } })}
                    className="card border h-100 rounded-4 text-center p-3 card-ayur shadow-xs"
                    style={{ cursor: 'pointer', background: '#fdfdfb' }}
                  >
                    <div 
                      className="rounded-circle bg-success-subtle text-success d-flex align-items-center justify-content-center mx-auto mb-2" 
                      style={{ width: '56px', height: '56px' }}
                    >
                      <FaMortarPestle size={22} />
                    </div>
                    <h6 className="fw-bold text-dark mb-0 small text-truncate" title={cat.name}>
                      {cat.name}
                    </h6>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </section>
      )}

      {/* 5. Featured Ayurvedic Remedies / Products */}
      {featuredProducts.length > 0 && (
        <section className="py-5 bg-light">
          <div className="container">
            <div className="d-flex justify-content-between align-items-end mb-4">
              <div>
                <span className="badge badge-gold mb-2">Classical Dispensary</span>
                <h2 className="display-6 fw-bold font-playfair mb-1" style={{ color: '#0f2c1d' }}>
                  Featured Ayurvedic Remedies
                </h2>
                <p className="text-muted mb-0 small">Prepared according to authentic classical methods with zero synthetic preservatives</p>
              </div>
              <button
                onClick={() => navigate('/get-all-products')}
                className="btn btn-outline-success rounded-pill px-4 btn-sm d-flex align-items-center gap-2 fw-semibold"
              >
                All Products <FaArrowRight size={12} />
              </button>
            </div>

            <div className="row g-4">
              {featuredProducts.map((prod, idx) => {
                const rawImg = prod.imageUrl || prod.imageURL;
                const imgSrc = rawImg 
                  ? `${Api.imageBaseUrl}${rawImg}` 
                  : 'https://images.unsplash.com/photo-1617897903246-719242758050?auto=format&fit=crop&w=600&q=80';

                return (
                  <div className="col-12 col-sm-6 col-lg-3" key={idx}>
                    <div className="product-card shadow-xs">
                      <div 
                        className="product-image-container cursor-pointer"
                        onClick={() => navigate('/show-product-details', { state: { product: prod } })}
                        style={{ cursor: 'pointer' }}
                      >
                        <img
                          src={imgSrc}
                          alt={prod.title}
                          className="product-image"
                          onError={(e) => {
                            e.target.onerror = null;
                            e.target.src = 'https://images.unsplash.com/photo-1617897903246-719242758050?auto=format&fit=crop&w=600&q=80';
                          }}
                        />
                        {prod.stock <= 5 && prod.stock > 0 ? (
                          <span className="badge bg-danger position-absolute top-0 end-0 m-2 rounded-pill small">
                            Only {prod.stock} Left
                          </span>
                        ) : prod.stock > 0 ? (
                          <span className="badge bg-success-subtle text-success position-absolute top-0 end-0 m-2 rounded-pill small">
                            In Stock
                          </span>
                        ) : (
                          <span className="badge bg-secondary position-absolute top-0 end-0 m-2 rounded-pill small">
                            Out of Stock
                          </span>
                        )}
                      </div>

                      <div className="p-3 d-flex flex-column flex-grow-1">
                        <small className="text-muted text-uppercase fw-bold" style={{ fontSize: '10px', letterSpacing: '0.05em' }}>
                          {prod.categoryName || prod.brand || 'The Great Ayurveda'}
                        </small>
                        
                        <h6 
                          className="fw-bold font-playfair text-dark mb-1 text-truncate cursor-pointer mt-1"
                          onClick={() => navigate('/show-product-details', { state: { product: prod } })}
                          title={prod.title}
                          style={{ cursor: 'pointer' }}
                        >
                          {prod.title}
                        </h6>

                        <p className="text-muted small mb-2 text-truncate-2" style={{ height: '36px', overflow: 'hidden', lineHeight: '1.4' }}>
                          {prod.description}
                        </p>

                        <div className="mb-2">
                          {starGenerate(prod.averageRating || 5)}
                        </div>

                        <div className="d-flex justify-content-between align-items-center mt-auto pt-2 border-top">
                          <span className="fs-5 fw-bold" style={{ color: '#1b4332' }}>
                            ₹{Number(prod.price).toFixed(2)}
                          </span>
                          <button
                            onClick={() => handleAddToCart(prod)}
                            className="btn btn-sm btn-primary-ayur text-white rounded-pill px-3 d-flex align-items-center gap-1 fw-semibold"
                            disabled={prod.stock <= 0}
                          >
                            <FaShoppingBag size={12} /> Add to Cart
                          </button>
                        </div>
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        </section>
      )}

      {/* 6. Verified Ayurvedic Doctor Directory Preview */}
      {activeDoctors.length > 0 && (
        <section className="py-5 bg-white">
          <div className="container">
            <div className="d-flex justify-content-between align-items-end mb-4">
              <div>
                <span className="badge badge-ayur mb-2">Qualified Vaidyas</span>
                <h2 className="display-6 fw-bold font-playfair mb-1" style={{ color: '#0f2c1d' }}>
                  Consult Verified Ayurvedic Physicians
                </h2>
                <p className="text-muted mb-0 small">Schedule virtual or in-clinic consultations with certified BAMS practitioners</p>
              </div>
              <button
                onClick={() => navigate('/doctorconsult')}
                className="btn btn-outline-success rounded-pill px-4 btn-sm d-flex align-items-center gap-2 fw-semibold"
              >
                View All Doctors <FaArrowRight size={12} />
              </button>
            </div>

            <div className="row g-4">
              {activeDoctors.map((docItem, idx) => {
                const doctor = docItem.doctor || docItem;
                const details = docItem.doctorDetail || docItem.doctorDetails || {};
                const docImg = doctor.profileImage ? `${Api.imageBaseUrl}${doctor.profileImage}` : null;

                return (
                  <div className="col-12 col-md-4" key={idx}>
                    <div className="doctor-card shadow-xs">
                      {docImg ? (
                        <img
                          src={docImg}
                          alt={doctor.doctorName}
                          className="doctor-avatar"
                          onError={(e) => {
                            e.target.onerror = null;
                            e.target.src = 'https://images.unsplash.com/photo-1559839734-2b71ea197ec2?auto=format&fit=crop&w=300&q=80';
                          }}
                        />
                      ) : (
                        <div 
                          className="rounded-circle bg-success text-white d-flex align-items-center justify-content-center mx-auto mb-3 shadow-sm fw-bold fs-3"
                          style={{ width: '96px', height: '96px' }}
                        >
                          {doctor.doctorName ? doctor.doctorName.charAt(0) : 'Dr'}
                        </div>
                      )}

                      <h5 className="fw-bold font-playfair text-dark mb-1">{doctor.doctorName}</h5>
                      <span className="badge badge-ayur mx-auto mb-2 small">
                        {details.specializationName || details.specialization || "Ayurvedic Medicine"}
                      </span>
                      
                      <p className="text-muted small mb-3">
                        {details.experience || "10+"} Years Experience • {details.language || "Hindi, English, Sanskrit"}
                      </p>

                      <div className="bg-light p-2 rounded-3 small text-muted text-truncate mb-3" title={details.clinicAddress || "Wellness Center"}>
                        📍 {details.clinicAddress || "New Delhi Consultation Center"}
                      </div>

                      <button
                        onClick={() => navigate('/doctorconsult')}
                        className="btn-ayur-primary w-100 mt-auto py-2 small"
                      >
                        Book Consultation
                      </button>
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        </section>
      )}

      {/* 7. Therapeutic Yoga & Pranayama Practice */}
      {yogaList.length > 0 && (
        <section className="py-5 bg-light">
          <div className="container">
            <div className="d-flex justify-content-between align-items-end mb-4">
              <div>
                <span className="badge badge-gold mb-2">Yoga Chikitsa</span>
                <h2 className="display-6 fw-bold font-playfair mb-1" style={{ color: '#0f2c1d' }}>
                  Therapeutic Asana & Pranayama Tutorials
                </h2>
                <p className="text-muted mb-0 small">Calibrated postures designed to balance neurological prana and release chronic strain</p>
              </div>
              <button
                onClick={() => navigate('/yoga')}
                className="btn btn-outline-success rounded-pill px-4 btn-sm d-flex align-items-center gap-2 fw-semibold"
              >
                All Yoga Sequences <FaArrowRight size={12} />
              </button>
            </div>

            <div className="row g-4">
              {yogaList.map((yoga, idx) => (
                <div className="col-12 col-md-4" key={idx}>
                  <div className="card h-100 border-0 shadow-xs rounded-4 overflow-hidden card-ayur d-flex flex-column bg-white">
                    <img
                      src={yoga.imageUrl || 'https://images.unsplash.com/photo-1544367567-0f2fcb009e0b?auto=format&fit=crop&w=600&q=80'}
                      alt={yoga.yogaName}
                      className="card-img-top object-fit-cover"
                      style={{ height: '210px' }}
                      onError={(e) => {
                        e.target.onerror = null;
                        e.target.src = 'https://images.unsplash.com/photo-1544367567-0f2fcb009e0b?auto=format&fit=crop&w=600&q=80';
                      }}
                    />
                    <div className="card-body p-4 d-flex flex-column flex-grow-1">
                      <h5 className="fw-bold font-playfair text-dark mb-1">{yoga.yogaName}</h5>
                      <p className="text-muted small text-truncate-2 mb-3">
                        {yoga.Description || yoga.description}
                      </p>
                      <button
                        onClick={() => navigate('/ShowYogaDetails', { state: { yoga } })}
                        className="btn btn-outline-success rounded-pill btn-sm mt-auto fw-semibold"
                      >
                        Watch Video Guide
                      </button>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </section>
      )}

      {/* 8. Holistic Health Finder by Health Condition */}
      <section className="py-5 bg-white border-top">
        <div className="container text-center">
          <span className="badge badge-ayur mb-2">Holistic Health Navigator</span>
          <h2 className="display-6 fw-bold font-playfair text-dark mb-2">
            Targeted Relief by Health Condition
          </h2>
          <p className="text-muted mx-auto mb-4" style={{ maxWidth: '640px' }}>
            Explore verified classical remedies, dietary guidelines, and dinacharya routines calibrated for common contemporary health concerns.
          </p>

          <div className="d-flex flex-wrap justify-content-center gap-3">
            {[
              { label: "Digestive & Acidity", query: "Digestive" },
              { label: "Joint & Bone Care", query: "Joint" },
              { label: "Hair & Skin Vitality", query: "Hair" },
              { label: "Mental Focus & Stress", query: "Mental" },
              { label: "Respiratory & Lung Health", query: "Respiratory" },
              { label: "Immunity & Rejuvenation", query: "Immunity" }
            ].map((cond, cIdx) => (
              <button
                key={cIdx}
                onClick={() => navigate('/diseases', { state: { searchValue: cond.query } })}
                className="category-pill shadow-xs"
              >
                <FaLeaf size={12} className="text-success" />
                <span>{cond.label}</span>
              </button>
            ))}
          </div>
        </div>
      </section>

      {/* 9. Responsible Ayurveda Information & Medical Disclaimer */}
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

      {/* 10. Newsletter Dispatch Strip */}
      <section className="py-5 bg-white border-top">
        <div className="container">
          <div className="p-4 p-md-5 rounded-4 shadow-sm text-center text-white" style={{ background: 'linear-gradient(135deg, #0f2c1d 0%, #1b4332 60%, #2d6a4f 100%)' }}>
            <span className="badge badge-gold mb-2 d-inline-flex align-items-center gap-2">
              <FaEnvelope /> The Vedic Health Chronicle
            </span>
            <h3 className="display-6 fw-bold font-playfair mb-2">
              Receive Seasonal Ayurvedic Guidance
            </h3>
            <p className="text-light opacity-90 mx-auto mb-4" style={{ maxWidth: '600px' }}>
              Subscribe to receive authentic seasonal Ritucharya routines, herbal decoction recipes, and Vaidya health advisories directly in your inbox.
            </p>

            {newsletterSubscribed ? (
              <div className="alert alert-success d-inline-block rounded-pill px-4 py-2 small fw-semibold">
                ✓ Thank you! You will receive our next seasonal health edition.
              </div>
            ) : (
              <form onSubmit={handleNewsletterSubmit} className="d-flex justify-content-center gap-2 mx-auto" style={{ maxWidth: '480px' }}>
                <input
                  type="email"
                  required
                  placeholder="Enter your email address..."
                  className="form-control rounded-pill px-4 py-2 border-0"
                  value={newsletterEmail}
                  onChange={(e) => setNewsletterEmail(e.target.value)}
                />
                <button type="submit" className="btn-ayur-gold px-4 py-2 flex-shrink-0">
                  <FaPaperPlane size={13} /> Subscribe
                </button>
              </form>
            )}
          </div>
        </div>
      </section>
    </div>
  );
}

export default HomeData;