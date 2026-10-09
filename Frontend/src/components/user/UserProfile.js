import React, { useEffect, useState } from "react";
import Api, { apiClient } from "../WebApi/Api";
import { toast } from "react-toastify";
import { useNavigate, Link } from "react-router-dom";
import { 
  FaUser, FaEnvelope, FaPhone, FaCalendarCheck, 
  FaShoppingBag, FaSignOutAlt, FaClock, FaCheckCircle, 
  FaUserMd, FaMapMarkerAlt 
} from "react-icons/fa";

function UserProfile() {
  const navigate = useNavigate();
  const [user, setUser] = useState(null);
  const [appointments, setAppointments] = useState([]);
  const [loadingAppointments, setLoadingAppointments] = useState(true);
  const [activeTab, setActiveTab] = useState("appointments");

  // Edit profile state
  const [name, setName] = useState("");
  const [contact, setContact] = useState("");
  const [updating, setUpdating] = useState(false);

  useEffect(() => {
    const storedUser = JSON.parse(sessionStorage.getItem('user-details') || 'null');
    if (!storedUser) {
      toast.warning("Please sign in to view your profile");
      navigate('/user-account');
      return;
    }
    setUser(storedUser);
    setName(storedUser.username || "");
    setContact(storedUser.contact || "");
    loadAppointments(storedUser.id || storedUser._id);
  }, [navigate]);

  const loadAppointments = async (userId) => {
    try {
      setLoadingAppointments(true);
      const url = `${Api.getUserAppointments}/${userId}`;
      const response = await apiClient.get(url);
      setAppointments(response.data.appointments || []);
    } catch (error) {
      console.error("Failed to load appointments:", error);
    } finally {
      setLoadingAppointments(false);
    }
  };

  const handleUpdateProfile = async (e) => {
    e.preventDefault();
    if (!user) return;
    try {
      setUpdating(true);
      const formData = new FormData();
      formData.append('userId', user.id || user._id);
      formData.append('username', name.trim());
      formData.append('contact', contact.trim());

      const response = await apiClient.post(Api.userUpdateProfile, formData);
      const updated = response.data.user;
      setUser(updated);
      sessionStorage.setItem('user-details', JSON.stringify(updated));
      toast.success("Profile updated successfully!");
    } catch (error) {
      console.error("Update profile error:", error);
      const msg = error.response?.data?.message || "Failed to update profile";
      toast.error(msg);
    } finally {
      setUpdating(false);
    }
  };

  const handleLogout = () => {
    sessionStorage.clear();
    toast.info("Logged out successfully");
    navigate('/');
  };

  if (!user) return null;

  return (
    <div className="container py-5">
      <div className="row g-4">
        {/* Left Column: User Summary Card */}
        <div className="col-12 col-lg-4">
          <div className="card border-0 shadow-sm rounded-4 p-4 text-center bg-white">
            <div 
              className="rounded-circle bg-success text-white d-flex align-items-center justify-content-center mx-auto mb-3 shadow-sm fw-bold" 
              style={{ width: '84px', height: '84px', fontSize: '32px' }}
            >
              {user.username ? user.username.charAt(0).toUpperCase() : 'U'}
            </div>
            <h4 className="fw-bold font-playfair text-dark mb-1">{user.username}</h4>
            <p className="text-muted small mb-3">{user.email}</p>

            <div className="border-top pt-3 text-start small mb-4">
              <div className="d-flex align-items-center gap-2 mb-2 text-secondary">
                <FaEnvelope className="text-success" />
                <span>{user.email}</span>
              </div>
              <div className="d-flex align-items-center gap-2 mb-2 text-secondary">
                <FaPhone className="text-success" />
                <span>{user.contact || 'No contact registered'}</span>
              </div>
            </div>

            <div className="d-grid gap-2">
              <Link to="/order-details" className="btn btn-outline-success btn-sm rounded-pill py-2 fw-semibold">
                <FaShoppingBag className="me-2" /> My Orders & Purchases
              </Link>
              <Link to="/doctorconsult" className="btn-ayur-primary btn-sm py-2">
                <FaUserMd className="me-2" /> Book Doctor Consultation
              </Link>
              <button onClick={handleLogout} className="btn btn-outline-danger btn-sm rounded-pill py-2 mt-2">
                <FaSignOutAlt className="me-2" /> Sign Out
              </button>
            </div>
          </div>
        </div>

        {/* Right Column: Tabbed Content (Appointments & Edit Details) */}
        <div className="col-12 col-lg-8">
          <div className="card border-0 shadow-sm rounded-4 p-4 bg-white">
            {/* Tab Navigation */}
            <div className="d-flex gap-2 border-bottom pb-3 mb-4">
              <button
                className={`btn btn-sm rounded-pill px-4 fw-semibold ${activeTab === 'appointments' ? 'btn-success' : 'btn-light'}`}
                onClick={() => setActiveTab('appointments')}
              >
                <FaCalendarCheck className="me-2" /> Doctor Consultations ({appointments.length})
              </button>
              <button
                className={`btn btn-sm rounded-pill px-4 fw-semibold ${activeTab === 'edit' ? 'btn-success' : 'btn-light'}`}
                onClick={() => setActiveTab('edit')}
              >
                <FaUser className="me-2" /> Edit Account Info
              </button>
            </div>

            {/* Tab 1: Appointments List */}
            {activeTab === 'appointments' && (
              <div>
                <div className="d-flex justify-content-between align-items-center mb-3">
                  <h5 className="fw-bold font-playfair text-dark mb-0">Consultation History</h5>
                  <button 
                    onClick={() => loadAppointments(user.id || user._id)} 
                    className="btn btn-sm btn-link text-success p-0 text-decoration-none"
                  >
                    Refresh Status
                  </button>
                </div>

                {loadingAppointments ? (
                  <div className="text-center py-4">
                    <div className="spinner-border spinner-border-sm text-success" role="status"></div>
                  </div>
                ) : appointments.length === 0 ? (
                  <div className="text-center py-5 bg-light rounded-4 p-4">
                    <FaCalendarCheck size={40} className="text-muted mb-2" />
                    <h6 className="fw-bold text-dark mb-1">No Consultations Scheduled</h6>
                    <p className="text-muted small mb-3">You have not booked any appointments with our certified Vaidyas yet.</p>
                    <Link to="/doctorconsult" className="btn-ayur-primary btn-sm">
                      Consult a Certified Doctor
                    </Link>
                  </div>
                ) : (
                  <div className="space-y-3">
                    {appointments.map((apt) => (
                      <div key={apt.id} className="p-3 border rounded-3 mb-3 bg-light">
                        <div className="d-flex flex-wrap justify-content-between align-items-center mb-2">
                          <div>
                            <strong className="text-dark fs-6 font-playfair">{apt.doctorName}</strong>
                            <small className="text-muted d-block">{apt.doctorSpecialization || "Ayurvedic Practitioner"}</small>
                          </div>
                          <div>
                            {apt.status === "Pending" ? (
                              <span className="badge bg-warning text-dark px-3 py-1 rounded-pill small">
                                <FaClock className="me-1" /> Pending Approval
                              </span>
                            ) : apt.status === "Accepted" || apt.status === "Confirmed" ? (
                              <span className="badge bg-success px-3 py-1 rounded-pill small">
                                <FaCheckCircle className="me-1" /> Confirmed
                              </span>
                            ) : (
                              <span className="badge bg-secondary px-3 py-1 rounded-pill small">
                                {apt.status}
                              </span>
                            )}
                          </div>
                        </div>

                        <div className="small text-muted mb-2">
                          <span>📅 <strong>Date:</strong> {new Date(apt.appointmentDate).toLocaleDateString()}</span>
                          <span className="mx-2">•</span>
                          <span>⏰ <strong>Time:</strong> {apt.timeSlot}</span>
                        </div>

                        {apt.doctorClinicAddress && (
                          <div className="small text-muted mb-2">
                            <FaMapMarkerAlt className="text-success me-1" />
                            <span>{apt.doctorClinicAddress}</span>
                          </div>
                        )}

                        {apt.healthConcern && (
                          <div className="p-2 bg-white rounded-2 small border text-muted">
                            <strong>Health Concern:</strong> {apt.healthConcern}
                          </div>
                        )}
                      </div>
                    ))}
                  </div>
                )}
              </div>
            )}

            {/* Tab 2: Edit Account Details Form */}
            {activeTab === 'edit' && (
              <form onSubmit={handleUpdateProfile}>
                <h5 className="fw-bold font-playfair text-dark mb-3">Update Personal Details</h5>
                <div className="row g-3">
                  <div className="col-12 col-md-6">
                    <label className="form-label small fw-semibold text-dark">Full Name</label>
                    <input
                      type="text"
                      required
                      className="form-control rounded-3"
                      value={name}
                      onChange={(e) => setName(e.target.value)}
                    />
                  </div>

                  <div className="col-12 col-md-6">
                    <label className="form-label small fw-semibold text-dark">Contact Phone</label>
                    <input
                      type="tel"
                      className="form-control rounded-3"
                      value={contact}
                      onChange={(e) => setContact(e.target.value)}
                      placeholder="+91 9876543210"
                    />
                  </div>

                  <div className="col-12 col-md-6">
                    <label className="form-label small fw-semibold text-dark">Email Address</label>
                    <input
                      type="email"
                      disabled
                      className="form-control rounded-3 bg-light"
                      value={user.email}
                    />
                    <small className="text-muted" style={{ fontSize: '11px' }}>Account email cannot be modified</small>
                  </div>

                  <div className="col-12 mt-4">
                    <button
                      type="submit"
                      disabled={updating}
                      className="btn-ayur-primary px-4 py-2"
                    >
                      {updating ? "Saving Changes..." : "Update Profile"}
                    </button>
                  </div>
                </div>
              </form>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

export default UserProfile;
