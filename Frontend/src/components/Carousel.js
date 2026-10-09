import React from "react";
import { useNavigate } from "react-router-dom";
import { FaLeaf, FaUserMd, FaSpa, FaArrowRight } from "react-icons/fa";

function Carousel() {
  const navigate = useNavigate();

  return (
    <div id="ayurvedaHeroCarousel" className="carousel slide carousel-fade shadow-sm" data-bs-ride="carousel" data-bs-interval="6000">
      {/* Custom Clean Indicators */}
      <div className="carousel-indicators mb-3">
        <button
          type="button"
          data-bs-target="#ayurvedaHeroCarousel"
          data-bs-slide-to="0"
          className="active rounded-pill"
          style={{ width: '28px', height: '6px', transition: 'all 0.3s' }}
          aria-current="true"
          aria-label="Slide 1"
        ></button>
        <button
          type="button"
          data-bs-target="#ayurvedaHeroCarousel"
          data-bs-slide-to="1"
          className="rounded-pill"
          style={{ width: '28px', height: '6px', transition: 'all 0.3s' }}
          aria-label="Slide 2"
        ></button>
        <button
          type="button"
          data-bs-target="#ayurvedaHeroCarousel"
          data-bs-slide-to="2"
          className="rounded-pill"
          style={{ width: '28px', height: '6px', transition: 'all 0.3s' }}
          aria-label="Slide 3"
        ></button>
      </div>

      <div className="carousel-inner">
        {/* Slide 1: Authentic Vedic Herbal Apothecary */}
        <div className="carousel-item active">
          <div
            className="hero-editorial d-flex align-items-center py-5"
            style={{
              background: 'linear-gradient(90deg, rgba(15, 44, 29, 0.88) 0%, rgba(27, 67, 50, 0.65) 50%, rgba(27, 67, 50, 0.3) 100%), url("https://images.unsplash.com/photo-1617897903246-719242758050?auto=format&fit=crop&w=1800&q=85") center/cover no-repeat',
              minHeight: '520px'
            }}
          >
            <div className="container py-4">
              <div className="row align-items-center">
                <div className="col-12 col-md-9 col-lg-7">
                  <div className="hero-editorial-glass">
                    <span className="badge badge-gold px-3 py-2 rounded-pill fw-bold mb-3 d-inline-flex align-items-center gap-2">
                      <FaLeaf className="text-success" /> Sacred Vedic Healing & GMP Certified
                    </span>
                    <h1 className="display-5 fw-bold font-playfair mb-3 text-dark leading-tight" style={{ color: '#0f2c1d' }}>
                      Nourish Your Body, <br />
                      <span style={{ color: '#c9933b' }}>Harmonize Your Mind</span>
                    </h1>
                    <p className="lead text-muted mb-4 small fs-6" style={{ lineHeight: '1.7' }}>
                      Authentic classical Ayurvedic formulations, certified BAMS doctor pulse consultations, and holistic herbal lifestyle therapies to restore pure vitality.
                    </p>
                    <div className="d-flex flex-wrap gap-3">
                      <button
                        onClick={() => navigate('/get-all-products')}
                        className="btn-ayur-gold"
                      >
                        Explore Medicines <FaArrowRight size={13} />
                      </button>
                      <button
                        onClick={() => navigate('/doctorconsult')}
                        className="btn-ayur-outline"
                      >
                        Consult a Vaidya
                      </button>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* Slide 2: Verified Ayurvedic Vaidya Consultations */}
        <div className="carousel-item">
          <div
            className="hero-editorial d-flex align-items-center py-5"
            style={{
              background: 'linear-gradient(90deg, rgba(15, 44, 29, 0.9) 0%, rgba(20, 50, 35, 0.65) 50%, rgba(20, 50, 35, 0.25) 100%), url("https://images.unsplash.com/photo-1576091160399-112ba8d25d1d?auto=format&fit=crop&w=1800&q=85") center/cover no-repeat',
              minHeight: '520px'
            }}
          >
            <div className="container py-4">
              <div className="row align-items-center">
                <div className="col-12 col-md-9 col-lg-7">
                  <div className="hero-editorial-glass">
                    <span className="badge badge-ayur px-3 py-2 rounded-pill fw-bold mb-3 d-inline-flex align-items-center gap-2">
                      <FaUserMd /> Certified Ayurvedic Doctors
                    </span>
                    <h1 className="display-5 fw-bold font-playfair mb-3 text-dark leading-tight" style={{ color: '#0f2c1d' }}>
                      Personalized Pulse & <br />
                      <span style={{ color: '#2d6a4f' }}>Tridosha Chikitsa</span>
                    </h1>
                    <p className="lead text-muted mb-4 small fs-6" style={{ lineHeight: '1.7' }}>
                      Schedule 1-on-1 consultations with verified BAMS & MD specialists. Receive tailored diet routines, custom herbal Kashayams, and lifestyle prescriptions.
                    </p>
                    <div className="d-flex flex-wrap gap-3">
                      <button
                        onClick={() => navigate('/doctorconsult')}
                        className="btn-ayur-primary"
                      >
                        Book Consultation <FaArrowRight size={13} />
                      </button>
                      <button
                        onClick={() => navigate('/diseases')}
                        className="btn-ayur-outline"
                      >
                        Herbal Remedies Guide
                      </button>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* Slide 3: Therapeutic Yoga & Cellular Rejuvenation */}
        <div className="carousel-item">
          <div
            className="hero-editorial d-flex align-items-center py-5"
            style={{
              background: 'linear-gradient(90deg, rgba(15, 44, 29, 0.88) 0%, rgba(45, 106, 79, 0.6) 50%, rgba(45, 106, 79, 0.25) 100%), url("https://images.unsplash.com/photo-1545205597-3d9d02c29597?auto=format&fit=crop&w=1800&q=85") center/cover no-repeat',
              minHeight: '520px'
            }}
          >
            <div className="container py-4">
              <div className="row align-items-center">
                <div className="col-12 col-md-9 col-lg-7">
                  <div className="hero-editorial-glass">
                    <span className="badge badge-gold px-3 py-2 rounded-pill fw-bold mb-3 d-inline-flex align-items-center gap-2">
                      <FaSpa className="text-warning" /> Therapeutic Asanas & Pranayama
                    </span>
                    <h1 className="display-5 fw-bold font-playfair mb-3 text-dark leading-tight" style={{ color: '#0f2c1d' }}>
                      Therapeutic Asanas for <br />
                      <span style={{ color: '#c9933b' }}>Cellular Rejuvenation</span>
                    </h1>
                    <p className="lead text-muted mb-4 small fs-6" style={{ lineHeight: '1.7' }}>
                      Curated video tutorials and step-by-step posture guidance designed to relieve stress, enhance digestion, and promote neuromuscular equilibrium.
                    </p>
                    <div className="d-flex flex-wrap gap-3">
                      <button
                        onClick={() => navigate('/yoga')}
                        className="btn-ayur-gold"
                      >
                        Explore Yoga Practices <FaArrowRight size={13} />
                      </button>
                      <button
                        onClick={() => navigate('/get-all-products')}
                        className="btn-ayur-outline"
                      >
                        Shop Natural Remedies
                      </button>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* Modern carousel controls */}
      <button 
        className="carousel-control-prev" 
        type="button" 
        data-bs-target="#ayurvedaHeroCarousel" 
        data-bs-slide="prev"
        style={{ width: '50px', opacity: 0.8 }}
      >
        <span className="carousel-control-prev-icon p-3 rounded-circle bg-dark bg-opacity-25" aria-hidden="true"></span>
        <span className="visually-hidden">Previous</span>
      </button>
      <button 
        className="carousel-control-next" 
        type="button" 
        data-bs-target="#ayurvedaHeroCarousel" 
        data-bs-slide="next"
        style={{ width: '50px', opacity: 0.8 }}
      >
        <span className="carousel-control-next-icon p-3 rounded-circle bg-dark bg-opacity-25" aria-hidden="true"></span>
        <span className="visually-hidden">Next</span>
      </button>
    </div>
  );
}

export default Carousel;