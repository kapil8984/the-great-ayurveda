import React, { useState } from 'react';
import { toast } from 'react-toastify';
import { 
  FaMapMarkerAlt, FaPhoneAlt, FaEnvelope, FaClock, 
  FaPaperPlane, FaShieldAlt, FaComments 
} from 'react-icons/fa';

function Contact() {
  const [formData, setFormData] = useState({
    name: '',
    email: '',
    phone: '',
    subject: 'General Inquiry',
    message: ''
  });
  const [submitted, setSubmitted] = useState(false);

  const handleSubmit = (e) => {
    e.preventDefault();
    if (!formData.name.trim() || !formData.email.trim() || !formData.message.trim()) {
      toast.warning('Please complete all required fields.');
      return;
    }

    setSubmitted(true);
    toast.success('Thank you! Your inquiry has been sent to our Ayurvedic care sanctuary.');
    setFormData({ name: '', email: '', phone: '', subject: 'General Inquiry', message: '' });
  };

  return (
    <div className="bg-white">
      {/* Banner */}
      <section 
        className="py-5 text-white" 
        style={{ 
          background: 'linear-gradient(135deg, #0f2c1d 0%, #1b4332 60%, #2d6a4f 100%)',
          minHeight: '280px',
          display: 'flex',
          alignItems: 'center'
        }}
      >
        <div className="container py-3 text-center">
          <span className="badge badge-gold px-3 py-2 rounded-pill fw-bold mb-3 d-inline-flex align-items-center gap-2">
            <FaComments /> Sanctuary Support & Patient Assistance
          </span>
          <h1 className="display-4 fw-bold font-playfair text-white mb-2">Connect with Our Sanctuary</h1>
          <p className="lead mx-auto text-light opacity-90 small fs-6" style={{ maxWidth: '640px' }}>
            Whether you seek advice on herbal remedies, require appointment assistance, or have questions regarding product authenticity, our dedicated Vaidyas and patient coordinators are at your service.
          </p>
        </div>
      </section>

      {/* Main Form & Info Section */}
      <section className="py-5">
        <div className="container">
          <div className="row g-5">
            {/* Contact Details Card */}
            <div className="col-12 col-lg-5">
              <div className="p-4 p-md-5 rounded-4 bg-light border shadow-sm h-100 d-flex flex-column">
                <span className="badge badge-ayur w-fit mb-3">Headquarters & Wellness Center</span>
                <h3 className="fw-bold font-playfair mb-4" style={{ color: '#0f2c1d' }}>
                  The Great Ayurveda Sanctuary
                </h3>

                <div className="space-y-4 mb-4">
                  <div className="d-flex align-items-start gap-3 mb-3">
                    <div className="rounded-circle bg-success-subtle text-success p-3 flex-shrink-0">
                      <FaMapMarkerAlt size={20} />
                    </div>
                    <div>
                      <strong className="d-block text-dark">Sanctuary Address</strong>
                      <span className="text-muted small">
                        Ayurveda Bhavan, Holistic Wellness Marg, Barakhamba Road, Connaught Place, New Delhi - 110001, India
                      </span>
                    </div>
                  </div>

                  <div className="d-flex align-items-start gap-3 mb-3">
                    <div className="rounded-circle bg-success-subtle text-success p-3 flex-shrink-0">
                      <FaPhoneAlt size={20} />
                    </div>
                    <div>
                      <strong className="d-block text-dark">National Herbal Helpline</strong>
                      <span className="text-muted small d-block">+91 1800-AYUR-VEDA (Toll Free)</span>
                      <span className="text-muted small">+91 11-2345-6789 (Appointments)</span>
                    </div>
                  </div>

                  <div className="d-flex align-items-start gap-3 mb-3">
                    <div className="rounded-circle bg-success-subtle text-success p-3 flex-shrink-0">
                      <FaEnvelope size={20} />
                    </div>
                    <div>
                      <strong className="d-block text-dark">Electronic Mail</strong>
                      <span className="text-muted small d-block">care@thegreatayurveda.com</span>
                      <span className="text-muted small">practitioners@thegreatayurveda.com</span>
                    </div>
                  </div>

                  <div className="d-flex align-items-start gap-3">
                    <div className="rounded-circle bg-success-subtle text-success p-3 flex-shrink-0">
                      <FaClock size={20} />
                    </div>
                    <div>
                      <strong className="d-block text-dark">Clinic & Dispatch Timings</strong>
                      <span className="text-muted small d-block">Monday – Saturday: 8:00 AM – 8:00 PM IST</span>
                      <span className="text-muted small">Sunday: 9:00 AM – 2:00 PM (Emergency Herbal Care)</span>
                    </div>
                  </div>
                </div>

                <div className="mt-auto pt-3 border-top">
                  <div className="d-flex align-items-center gap-2 text-success small fw-semibold">
                    <FaShieldAlt /> <span>Government Ayush Ministry Compliant Facility</span>
                  </div>
                </div>
              </div>
            </div>

            {/* Inquiries Form */}
            <div className="col-12 col-lg-7">
              <div className="p-4 p-md-5 rounded-4 bg-white border shadow-sm">
                <h3 className="fw-bold font-playfair mb-2" style={{ color: '#0f2c1d' }}>
                  Send an Inquiry or Feedback
                </h3>
                <p className="text-muted small mb-4">
                  Please fill out the form below. A senior herbal coordinator or clinical Vaidya will respond within 24 hours.
                </p>

                {submitted ? (
                  <div className="alert alert-success rounded-4 p-4 text-center my-4">
                    <h5 className="fw-bold">Inquiry Received with Namaste!</h5>
                    <p className="mb-3 small">We have received your message and sent a confirmation acknowledgment to your email.</p>
                    <button onClick={() => setSubmitted(false)} className="btn btn-sm btn-outline-success rounded-pill px-4">
                      Send Another Message
                    </button>
                  </div>
                ) : (
                  <form onSubmit={handleSubmit}>
                    <div className="row g-3">
                      <div className="col-12 col-md-6">
                        <label className="form-label small fw-semibold text-dark">Your Name *</label>
                        <input
                          type="text"
                          required
                          className="form-control rounded-3"
                          placeholder="e.g. Priyanshu Sharma"
                          value={formData.name}
                          onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                        />
                      </div>

                      <div className="col-12 col-md-6">
                        <label className="form-label small fw-semibold text-dark">Email Address *</label>
                        <input
                          type="email"
                          required
                          className="form-control rounded-3"
                          placeholder="e.g. name@example.com"
                          value={formData.email}
                          onChange={(e) => setFormData({ ...formData, email: e.target.value })}
                        />
                      </div>

                      <div className="col-12 col-md-6">
                        <label className="form-label small fw-semibold text-dark">Contact Phone</label>
                        <input
                          type="tel"
                          className="form-control rounded-3"
                          placeholder="e.g. +91 9876543210"
                          value={formData.phone}
                          onChange={(e) => setFormData({ ...formData, phone: e.target.value })}
                        />
                      </div>

                      <div className="col-12 col-md-6">
                        <label className="form-label small fw-semibold text-dark">Subject / Concern</label>
                        <select
                          className="form-select rounded-3"
                          value={formData.subject}
                          onChange={(e) => setFormData({ ...formData, subject: e.target.value })}
                        >
                          <option value="General Inquiry">General Inquiry</option>
                          <option value="Doctor Appointment">Doctor Appointment Assistance</option>
                          <option value="Product Guidance">Herbal Medicine Guidance</option>
                          <option value="Order Tracking">Order & Delivery Query</option>
                          <option value="Practitioner Joining">Practitioner Registration</option>
                        </select>
                      </div>

                      <div className="col-12">
                        <label className="form-label small fw-semibold text-dark">Detailed Message *</label>
                        <textarea
                          rows="4"
                          required
                          className="form-control rounded-3"
                          placeholder="Describe your health question, order concern, or inquiry..."
                          value={formData.message}
                          onChange={(e) => setFormData({ ...formData, message: e.target.value })}
                        ></textarea>
                      </div>

                      <div className="col-12 mt-4">
                        <button type="submit" className="btn-ayur-primary w-100 py-3">
                          <FaPaperPlane size={14} /> Send Sacred Message
                        </button>
                      </div>
                    </div>
                  </form>
                )}
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* Frequently Asked Questions */}
      <section className="py-5 bg-light border-top">
        <div className="container">
          <div className="text-center mb-5">
            <span className="badge badge-ayur mb-2">Frequently Asked Questions</span>
            <h2 className="display-6 fw-bold font-playfair text-dark">Common Herbal Inquiries</h2>
            <p className="text-muted small mx-auto" style={{ maxWidth: '600px' }}>
              Clear answers regarding doctor consultation workflow, formulations, and order delivery.
            </p>
          </div>

          <div className="row justify-content-center">
            <div className="col-12 col-lg-8">
              <div className="accordion rounded-4 overflow-hidden border shadow-sm" id="faqAccordion">
                <div className="accordion-item border-0 border-bottom">
                  <h2 className="accordion-header" id="headingOne">
                    <button className="accordion-button fw-semibold text-dark" type="button" data-bs-toggle="collapse" data-bs-target="#collapseOne" aria-expanded="true">
                      How are doctor appointments confirmed?
                    </button>
                  </h2>
                  <div id="collapseOne" className="accordion-collapse collapse show" data-bs-parent="#faqAccordion">
                    <div className="accordion-body text-muted small">
                      When you submit an appointment request, our system validates doctor slot availability to prevent double-booking. The practitioner receives your request and accepts or updates the status. You can track status directly in your profile dashboard.
                    </div>
                  </div>
                </div>

                <div className="accordion-item border-0 border-bottom">
                  <h2 className="accordion-header" id="headingTwo">
                    <button className="accordion-button collapsed fw-semibold text-dark" type="button" data-bs-toggle="collapse" data-bs-target="#collapseTwo">
                      Are all products certified by regulatory authorities?
                    </button>
                  </h2>
                  <div id="collapseTwo" className="accordion-collapse collapse" data-bs-parent="#faqAccordion">
                    <div className="accordion-body text-muted small">
                      Yes. All classical Kashayams, Ghritams, Churnas, and Asavams offered on The Great Ayurveda are produced in GMP-certified facilities adhering to the Ayurvedic Pharmacopoeia of India (API) standards.
                    </div>
                  </div>
                </div>

                <div className="accordion-item border-0">
                  <h2 className="accordion-header" id="headingThree">
                    <button className="accordion-button collapsed fw-semibold text-dark" type="button" data-bs-toggle="collapse" data-bs-target="#collapseThree">
                      Can I take Ayurvedic remedies alongside my regular medications?
                    </button>
                  </h2>
                  <div id="collapseThree" className="accordion-collapse collapse" data-bs-parent="#faqAccordion">
                    <div className="accordion-body text-muted small">
                      Many herbal supplements can be integrated safely with a 1-hour interval, but certain potent herbs may interact with prescription drugs (such as blood thinners or diabetes medicines). We strongly recommend consulting our certified Vaidyas before combining regimens.
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>
    </div>
  );
}

export default Contact;
