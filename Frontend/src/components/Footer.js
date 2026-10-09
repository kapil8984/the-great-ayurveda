import React from "react";
import { Link } from "react-router-dom";
import { 
  FaPhoneAlt, FaEnvelope, FaMapMarkerAlt, 
  FaLock, FaCertificate 
} from "react-icons/fa";

function Footer() {
  const currentYear = new Date().getFullYear();

  return (
    <footer className="ayur-footer text-light">
      <div className="container">
        <div className="row g-4 mb-5">
          {/* Brand Info */}
          <div className="col-12 col-md-4">
            <div className="d-flex align-items-center gap-2 mb-3">
              <span className="fs-4 fw-bold font-cinzel text-warning" style={{ letterSpacing: '0.04em' }}>
                THE GREAT AYURVEDA
              </span>
            </div>
            <p className="text-light opacity-75 small pe-md-4 leading-relaxed mb-3">
              Reviving authentic classical Vedic healthcare for contemporary life. Preserving timeless Shastric formulations, pulse diagnosis (Nadi Pariksha) consultations with verified BAMS Vaidyas, and therapeutic yoga.
            </p>
            <div className="d-flex flex-column gap-2 text-warning small">
              <div className="d-flex align-items-center gap-2">
                <FaCertificate /> <span>GMP Certified & Heavy-Metal Tested Apothecary</span>
              </div>
              <div className="d-flex align-items-center gap-2">
                <FaLock /> <span>Secure Encrypted Health Consultations</span>
              </div>
            </div>
          </div>

          {/* Quick Explore Links */}
          <div className="col-6 col-md-2">
            <h5 className="fw-bold font-playfair text-white mb-3">Explore</h5>
            <ul className="list-unstyled space-y-2 small">
              <li className="mb-2"><Link to="/get-all-products">Herbal Pharmacy</Link></li>
              <li className="mb-2"><Link to="/doctorconsult">Consult Vaidyas</Link></li>
              <li className="mb-2"><Link to="/yoga">Therapeutic Yoga</Link></li>
              <li className="mb-2"><Link to="/diseases">Home Remedies Guide</Link></li>
              <li className="mb-2"><Link to="/about">About Ayurveda</Link></li>
            </ul>
          </div>

          {/* Professional Portals */}
          <div className="col-6 col-md-2">
            <h5 className="fw-bold font-playfair text-white mb-3">Portals</h5>
            <ul className="list-unstyled space-y-2 small">
              <li className="mb-2"><Link to="/profile">Patient Profile</Link></li>
              <li className="mb-2"><Link to="/order-details">Order Tracking</Link></li>
              <li className="mb-2"><Link to="/doctor-signIn">Doctor Portal</Link></li>
              <li className="mb-2"><Link to="/admin-signIn">Admin Panel</Link></li>
              <li className="mb-2"><Link to="/contact">Sanctuary Support</Link></li>
            </ul>
          </div>

          {/* Sanctuary Contact */}
          <div className="col-12 col-md-4">
            <h5 className="fw-bold font-playfair text-white mb-3">Ayurvedic Sanctuary</h5>
            <div className="small text-light opacity-80 space-y-2">
              <div className="d-flex align-items-start gap-2 mb-2">
                <FaMapMarkerAlt className="text-warning flex-shrink-0 mt-1" />
                <span>Ayurveda Bhavan, Holistic Wellness Marg, New Delhi - 110001, India</span>
              </div>
              <div className="d-flex align-items-center gap-2 mb-2">
                <FaPhoneAlt className="text-warning flex-shrink-0" />
                <span>+91 1800-AYUR-VEDA (Toll-Free National Helpline)</span>
              </div>
              <div className="d-flex align-items-center gap-2 mb-3">
                <FaEnvelope className="text-warning flex-shrink-0" />
                <span>care@thegreatayurveda.com</span>
              </div>
              <div className="p-2 rounded-3 bg-white bg-opacity-10 small text-light">
                <span>🕒 Consultations: Mon–Sat 8:00 AM – 8:00 PM IST</span>
              </div>
            </div>
          </div>
        </div>

        {/* Responsible Medical Disclaimer */}
        <div className="p-3 rounded-3 bg-black bg-opacity-25 border border-white border-opacity-10 mb-4 small text-light opacity-75">
          <strong className="text-warning">Responsible Health Notice:</strong> Content provided across this platform is for educational and Ayurvedic wellness rejuvenation purposes. It does not replace medical advice, emergency care, or prescription pharmaceuticals. Always consult a certified healthcare practitioner before altering medical treatments.
        </div>

        {/* Bottom Bar */}
        <div className="border-top border-white border-opacity-10 pt-4 text-center small text-light opacity-75 d-flex flex-wrap justify-content-between align-items-center gap-2">
          <p className="mb-0">
            © {currentYear} The Great Ayurveda. Sacred Healthcare & Herbal Wellness. All rights reserved.
          </p>
          <div className="d-flex gap-3">
            <Link to="/about" className="text-decoration-none text-light opacity-75 hover-text-warning">Philosophy</Link>
            <span>•</span>
            <Link to="/contact" className="text-decoration-none text-light opacity-75 hover-text-warning">Contact</Link>
            <span>•</span>
            <span className="text-light opacity-75">Ayush Standard Compliant</span>
          </div>
        </div>
      </div>
    </footer>
  );
}

export default Footer;