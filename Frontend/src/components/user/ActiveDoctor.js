import React, { useEffect, useState } from "react";
import Api, { apiClient } from "../WebApi/Api";
import { toast } from "react-toastify";
import { useNavigate } from "react-router-dom";
import { 
  FaUserMd, FaGraduationCap, FaMapMarkerAlt, 
  FaLanguage, FaCheckCircle, FaSearch, 
  FaCalendarCheck 
} from "react-icons/fa";

function ActiveDoctor() {
  const navigate = useNavigate();
  const [doctors, setDoctors] = useState([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState("");
  const [selectedDoctor, setSelectedDoctor] = useState(null);
  const [bookingSuccess, setBookingSuccess] = useState(false);
  const [bookingLoading, setBookingLoading] = useState(false);

  // Consultation form state
  const [patientName, setPatientName] = useState("");
  const [patientEmail, setPatientEmail] = useState("");
  const [patientContact, setPatientContact] = useState("");
  const [appointmentDate, setAppointmentDate] = useState("");
  const [timeSlot, setTimeSlot] = useState("Morning (9:00 AM - 12:00 PM)");
  const [problemDescription, setProblemDescription] = useState("");

  const currentUser = JSON.parse(sessionStorage.getItem('user-details') || 'null');

  useEffect(() => {
    loadDoctors();
    if (currentUser) {
      setPatientName(currentUser.username || "");
      setPatientEmail(currentUser.email || "");
      setPatientContact(currentUser.contact || "");
    }
  }, []);

  const loadDoctors = async () => {
    try {
      setLoading(true);
      const response = await apiClient.get(Api.getActiveDoctors);
      const list = response.data.doctors || response.data.doctorList || response.data || [];
      setDoctors(Array.isArray(list) ? list : []);
    } catch (error) {
      console.error("Failed to load doctors:", error);
      toast.error("Could not load verified Ayurvedic doctors");
    } finally {
      setLoading(false);
    }
  };

  const handleOpenModal = (doc) => {
    if (!currentUser) {
      toast.info("Please sign in or create an account to book a doctor consultation.");
      navigate('/user-account');
      return;
    }

    setSelectedDoctor(doc);
    setBookingSuccess(false);
    // Default appointment date to tomorrow
    const tomorrow = new Date();
    tomorrow.setDate(tomorrow.getDate() + 1);
    setAppointmentDate(tomorrow.toISOString().split('T')[0]);
  };

  const handleBookConsultation = async (e) => {
    e.preventDefault();
    if (!selectedDoctor) return;

    if (!currentUser) {
      toast.info("Please sign in to book your consultation");
      navigate('/user-account');
      return;
    }

    if (!patientName.trim() || !patientEmail.trim() || !patientContact.trim()) {
      toast.warning("Please fill all required patient details");
      return;
    }

    try {
      setBookingLoading(true);
      const doctor = selectedDoctor.doctor || selectedDoctor;
      const doctorId = doctor.id || doctor._id;
      const userId = currentUser.id || currentUser._id;
      
      const payload = {
        userId: userId,
        doctorId: doctorId,
        patientName: patientName.trim(),
        patientEmail: patientEmail.trim(),
        patientContact: patientContact.trim(),
        appointmentDate: new Date(appointmentDate).toISOString(),
        timeSlot: timeSlot,
        healthConcern: problemDescription.trim() || "Ayurvedic pulse & lifestyle health consultation"
      };

      const response = await apiClient.post(Api.bookAppointment, payload);
      setBookingSuccess(true);
      toast.success(response.data.message || "Consultation booked successfully! Your Vaidya will review your appointment.");
    } catch (error) {
      console.error("Booking error:", error);
      const msg = error.response?.data?.message || "Failed to book consultation. Please check slot availability.";
      toast.error(msg);
    } finally {
      setBookingLoading(false);
    }
  };

  const filteredDoctors = doctors.filter((item) => {
    const doc = item.doctor || item;
    const details = item.doctorDetail || item.doctorDetails || {};
    const docName = doc.doctorName?.toLowerCase() || "";
    const spec = (details.specializationName || details.specialization || "").toLowerCase();
    const lang = (details.language || "").toLowerCase();
    const q = searchTerm.toLowerCase();
    return docName.includes(q) || spec.includes(q) || lang.includes(q);
  });

  return (
    <div className="container-fluid py-5 px-3 px-md-5">
      {/* Header Banner */}
      <div className="text-center mb-5">
        <span className="badge badge-ayur mb-2">Ayurvedic Physicians</span>
        <h1 className="display-6 fw-bold font-playfair mb-2" style={{ color: '#0f2c1d' }}>
          Consult Verified Ayurvedic Vaidyas
        </h1>
        <p className="text-muted mx-auto" style={{ maxWidth: '680px' }}>
          Connect with government-approved BAMS & MD Ayurvedic practitioners for personalized pulse diagnosis (Nadi Pariksha), Prakriti dosha assessments, and targeted herbal healing programs.
        </p>

        {/* Search Bar */}
        <div className="mt-4 mx-auto" style={{ maxWidth: '520px' }}>
          <div className="position-relative">
            <input
              type="text"
              className="ayur-search-input pe-5"
              placeholder="Search doctor by name, specialization, or language..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
            />
            <span className="position-absolute end-0 top-50 translate-middle-y me-3 text-muted">
              <FaSearch />
            </span>
          </div>
        </div>
      </div>

      {loading ? (
        <div className="text-center py-5">
          <div className="spinner-border text-success" role="status">
            <span className="visually-hidden">Loading doctors...</span>
          </div>
          <p className="text-muted small mt-2">Loading verified Ayurvedic practitioners...</p>
        </div>
      ) : filteredDoctors.length === 0 ? (
        <div className="text-center py-5 bg-light rounded-4 my-4 p-5">
          <FaUserMd size={48} className="text-muted mb-3" />
          <h4 className="fw-bold font-playfair text-dark">No Doctors Found</h4>
          <p className="text-muted small">No verified Ayurvedic doctors match your current search terms.</p>
          <button onClick={() => setSearchTerm("")} className="btn-ayur-outline btn-sm">
            Reset Filters
          </button>
        </div>
      ) : (
        <div className="row g-4 justify-content-center">
          {filteredDoctors.map((item, idx) => {
            const doc = item.doctor || item;
            const details = item.doctorDetail || item.doctorDetails || {};
            const docImg = doc.profileImage ? `${Api.imageBaseUrl}${doc.profileImage}` : null;

            return (
              <div className="col-12 col-md-6 col-lg-4" key={idx}>
                <div className="doctor-card shadow-xs">
                  {docImg ? (
                    <img
                      src={docImg}
                      alt={doc.doctorName}
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
                      {doc.doctorName ? doc.doctorName.charAt(0) : 'Dr'}
                    </div>
                  )}

                  <h4 className="fw-bold font-playfair text-dark mb-1">{doc.doctorName}</h4>
                  
                  <div className="d-flex justify-content-center gap-2 mb-2">
                    <span className="badge badge-ayur small">
                      {details.specializationName || details.specialization || "General Ayurveda"}
                    </span>
                    <span className="badge badge-gold small">
                      <FaCheckCircle className="me-1" /> Verified BAMS
                    </span>
                  </div>

                  <div className="space-y-2 text-muted small text-start my-3 p-3 bg-light rounded-3">
                    <div className="d-flex align-items-center gap-2 mb-1">
                      <FaGraduationCap className="text-success flex-shrink-0" />
                      <span>{details.experience || "8+"} Years of Clinical Experience</span>
                    </div>
                    <div className="d-flex align-items-center gap-2 mb-1">
                      <FaLanguage className="text-success flex-shrink-0" />
                      <span>{details.language || "Hindi, English, Sanskrit"}</span>
                    </div>
                    <div className="d-flex align-items-center gap-2">
                      <FaMapMarkerAlt className="text-success flex-shrink-0" />
                      <span className="text-truncate" title={details.clinicAddress || "Sanctuary Clinic"}>
                        {details.clinicAddress || "Sanctuary Ayurvedic Wellness Center"}
                      </span>
                    </div>
                  </div>

                  <button
                    onClick={() => handleOpenModal(item)}
                    className="btn-ayur-primary w-100 py-2 mt-auto"
                  >
                    <FaCalendarCheck size={13} /> Schedule Consultation
                  </button>
                </div>
              </div>
            );
          })}
        </div>
      )}

      {/* Appointment Booking Modal */}
      {selectedDoctor && (
        <div 
          className="modal fade show d-block" 
          tabIndex="-1" 
          style={{ backgroundColor: 'rgba(15, 44, 29, 0.6)', backdropFilter: 'blur(4px)' }}
        >
          <div className="modal-dialog modal-dialog-centered modal-lg">
            <div className="modal-content shadow-lg border-0 rounded-4 overflow-hidden">
              <div className="modal-header bg-light p-4 border-bottom">
                <div>
                  <span className="badge badge-ayur mb-1">Clinical Appointment</span>
                  <h4 className="modal-title fw-bold font-playfair mb-0 text-dark">
                    Consult with {selectedDoctor.doctor?.doctorName || selectedDoctor.doctorName}
                  </h4>
                </div>
                <button
                  type="button"
                  className="btn-close"
                  onClick={() => setSelectedDoctor(null)}
                  aria-label="Close"
                ></button>
              </div>

              <div className="modal-body p-4">
                {bookingSuccess ? (
                  <div className="text-center py-4">
                    <div className="rounded-circle bg-success-subtle text-success p-3 d-inline-flex mb-3">
                      <FaCheckCircle size={40} />
                    </div>
                    <h3 className="fw-bold font-playfair text-dark mb-2">Consultation Request Confirmed!</h3>
                    <p className="text-muted small mx-auto mb-4" style={{ maxWidth: '500px' }}>
                      Your consultation details have been sent to <strong>{selectedDoctor.doctor?.doctorName || selectedDoctor.doctorName}</strong>. You can monitor confirmation and doctor notes in your patient profile.
                    </p>
                    <div className="d-flex justify-content-center gap-3">
                      <button onClick={() => navigate('/profile')} className="btn-ayur-primary btn-sm">
                        View My Appointments
                      </button>
                      <button onClick={() => setSelectedDoctor(null)} className="btn-ayur-outline btn-sm">
                        Close
                      </button>
                    </div>
                  </div>
                ) : (
                  <form onSubmit={handleBookConsultation}>
                    <div className="row g-3">
                      <div className="col-12 col-md-6">
                        <label className="form-label small fw-semibold text-dark">Patient Name *</label>
                        <input
                          type="text"
                          required
                          className="form-control rounded-3"
                          value={patientName}
                          onChange={(e) => setPatientName(e.target.value)}
                          placeholder="Full Name"
                        />
                      </div>

                      <div className="col-12 col-md-6">
                        <label className="form-label small fw-semibold text-dark">Email Address *</label>
                        <input
                          type="email"
                          required
                          className="form-control rounded-3"
                          value={patientEmail}
                          onChange={(e) => setPatientEmail(e.target.value)}
                          placeholder="email@example.com"
                        />
                      </div>

                      <div className="col-12 col-md-6">
                        <label className="form-label small fw-semibold text-dark">Contact Phone *</label>
                        <input
                          type="tel"
                          required
                          className="form-control rounded-3"
                          value={patientContact}
                          onChange={(e) => setPatientContact(e.target.value)}
                          placeholder="+91 9876543210"
                        />
                      </div>

                      <div className="col-12 col-md-6">
                        <label className="form-label small fw-semibold text-dark">Preferred Date *</label>
                        <input
                          type="date"
                          required
                          className="form-control rounded-3"
                          value={appointmentDate}
                          min={new Date().toISOString().split('T')[0]}
                          onChange={(e) => setAppointmentDate(e.target.value)}
                        />
                      </div>

                      <div className="col-12 col-md-6">
                        <label className="form-label small fw-semibold text-dark">Preferred Time Slot *</label>
                        <select
                          className="form-select rounded-3"
                          value={timeSlot}
                          onChange={(e) => setTimeSlot(e.target.value)}
                        >
                          <option value="Morning (9:00 AM - 12:00 PM)">Morning (9:00 AM - 12:00 PM)</option>
                          <option value="Afternoon (1:00 PM - 4:00 PM)">Afternoon (1:00 PM - 4:00 PM)</option>
                          <option value="Evening (5:00 PM - 8:00 PM)">Evening (5:00 PM - 8:00 PM)</option>
                        </select>
                      </div>

                      <div className="col-12 col-md-6">
                        <label className="form-label small fw-semibold text-dark">Consultation Mode</label>
                        <div className="p-2 border rounded-3 bg-light small text-muted">
                          Virtual Pulse & Video Telehealth / Clinic In-Person
                        </div>
                      </div>

                      <div className="col-12">
                        <label className="form-label small fw-semibold text-dark">Primary Health Concern or Symptoms</label>
                        <textarea
                          rows="3"
                          className="form-control rounded-3"
                          placeholder="Describe your health symptoms, chronicity, or dietary inquiries..."
                          value={problemDescription}
                          onChange={(e) => setProblemDescription(e.target.value)}
                        ></textarea>
                      </div>

                      <div className="col-12 mt-4 pt-3 border-top d-flex justify-content-end gap-2">
                        <button
                          type="button"
                          className="btn btn-light rounded-pill px-4"
                          onClick={() => setSelectedDoctor(null)}
                        >
                          Cancel
                        </button>
                        <button
                          type="submit"
                          className="btn-ayur-primary px-4"
                          disabled={bookingLoading}
                        >
                          {bookingLoading ? (
                            <>
                              <span className="spinner-border spinner-border-sm me-2" role="status"></span>
                              Validating Slot...
                            </>
                          ) : (
                            "Confirm Appointment"
                          )}
                        </button>
                      </div>
                    </div>
                  </form>
                )}
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export default ActiveDoctor;