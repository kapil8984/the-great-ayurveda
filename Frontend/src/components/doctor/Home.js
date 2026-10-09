import React, { useEffect, useState } from 'react';
import { Outlet, useNavigate, useLocation } from 'react-router-dom';
import Api, { apiClient } from '../WebApi/Api';
import { toast } from 'react-toastify';
import { FaUserMd, FaCalendarCheck, FaClock, FaCheckCircle, FaExclamationCircle } from 'react-icons/fa';

function Home() {
    const [doctor, setDoctor] = useState(null);
    const [doctorDetails, setDoctorDetails] = useState(null);
    const [appointments, setAppointments] = useState([]);
    const [loadingAppointments, setLoadingAppointments] = useState(true);
    const navigate = useNavigate();
    const location = useLocation();

    const isChildRoute = location.pathname.includes('/doctor-profile');

    useEffect(() => {
        const storedDoc = JSON.parse(sessionStorage.getItem('doctor-details') || 'null');
        if (!storedDoc) {
            navigate('/doctor-signIn');
            return;
        }
        setDoctor(storedDoc);
        const storedDetails = JSON.parse(sessionStorage.getItem('doctor-extra-details') || 'null');
        setDoctorDetails(storedDetails);

        if (!isChildRoute) {
            loadAppointments();
        }
    }, [navigate, isChildRoute]);

    const loadAppointments = async () => {
        try {
            setLoadingAppointments(true);
            const storedDoc = JSON.parse(sessionStorage.getItem('doctor-details') || 'null');
            const docId = storedDoc ? (storedDoc.id || storedDoc._id) : '';
            const url = docId ? `${Api.getDoctorAppointments}/${docId}` : Api.getDoctorAppointments;
            const response = await apiClient.get(url);
            setAppointments(response.data.appointments || []);
        } catch (error) {
            console.error("Doctor appointments error:", error);
        } finally {
            setLoadingAppointments(false);
        }
    };

    const handleUpdateAppointmentStatus = async (appointmentId, newStatus) => {
        try {
            await apiClient.put(`${Api.updateAppointmentStatus}/${appointmentId}/status`, { status: newStatus });
            toast.success(`Appointment marked as ${newStatus}`);
            loadAppointments();
        } catch (error) {
            console.error("Status update error:", error);
            toast.error("Could not update status");
        }
    };

    if (isChildRoute) {
        return (
            <div className="w-100 p-3">
                <Outlet />
            </div>
        );
    }

    const approvalStatus = doctorDetails?.status;

    return (
        <div className="container-fluid p-4">
            {/* Practitioner Status Alert */}
            {approvalStatus === 1 ? (
                <div className="alert alert-success rounded-4 d-flex align-items-center gap-3 mb-4 shadow-sm border-0">
                    <FaCheckCircle size={24} className="text-success" />
                    <div>
                        <strong className="d-block">Verified & Active Practitioner</strong>
                        <span className="small">Your profile and credentials have been verified by the administrator. You are currently active in the public doctor directory.</span>
                    </div>
                </div>
            ) : approvalStatus === 0 ? (
                <div className="alert alert-warning rounded-4 d-flex align-items-center gap-3 mb-4 shadow-sm border-0">
                    <FaClock size={24} className="text-warning" />
                    <div>
                        <strong className="d-block">Application Pending Admin Approval</strong>
                        <span className="small">Your credentials are under review by the administrator. Once approved, your clinic will appear on the public consultation page.</span>
                    </div>
                </div>
            ) : approvalStatus === -1 ? (
                <div className="alert alert-danger rounded-4 d-flex align-items-center gap-3 mb-4 shadow-sm border-0">
                    <FaExclamationCircle size={24} className="text-danger" />
                    <div>
                        <strong className="d-block">Verification Needs Attention</strong>
                        <span className="small">Your submitted credentials were rejected. Please review your details and re-upload valid BAMS/MD registration documents.</span>
                    </div>
                </div>
            ) : (
                <div className="alert alert-info rounded-4 d-flex align-items-center justify-content-between mb-4 shadow-sm border-0">
                    <div className="d-flex align-items-center gap-3">
                        <FaUserMd size={24} className="text-info" />
                        <div>
                            <strong className="d-block">Complete Your Practitioner Profile</strong>
                            <span className="small">Add your specialization, clinic address, and qualification documents to start receiving patients.</span>
                        </div>
                    </div>
                    <button
                        onClick={() => navigate('/doctor-dashboard/doctor-profile/add-details-doctor')}
                        className="btn btn-primary-ayur text-white rounded-pill btn-sm px-3 fw-semibold"
                    >
                        Add Details
                    </button>
                </div>
            )}

            {/* Dashboard Overview Cards */}
            <div className="row g-4 mb-4">
                <div className="col-12 col-md-4">
                    <div className="card border-0 shadow-sm rounded-4 p-4 card-ayur bg-white">
                        <div className="d-flex align-items-center justify-content-between mb-2">
                            <span className="text-muted small fw-semibold">Consultation Requests</span>
                            <div className="rounded-circle bg-primary-subtle text-primary p-3">
                                <FaCalendarCheck size={20} />
                            </div>
                        </div>
                        <h3 className="fw-bold text-dark mb-0 font-playfair">{appointments.length}</h3>
                        <small className="text-muted">Total booked sessions</small>
                    </div>
                </div>

                <div className="col-12 col-md-4">
                    <div className="card border-0 shadow-sm rounded-4 p-4 card-ayur bg-white">
                        <div className="d-flex align-items-center justify-content-between mb-2">
                            <span className="text-muted small fw-semibold">Pending Requests</span>
                            <div className="rounded-circle bg-warning-subtle text-warning p-3">
                                <FaClock size={20} />
                            </div>
                        </div>
                        <h3 className="fw-bold text-warning mb-0 font-playfair">
                            {appointments.filter(a => a.status === 'Pending').length}
                        </h3>
                        <small className="text-muted">Awaiting your confirmation</small>
                    </div>
                </div>

                <div className="col-12 col-md-4">
                    <div className="card border-0 shadow-sm rounded-4 p-4 card-ayur bg-white">
                        <div className="d-flex align-items-center justify-content-between mb-2">
                            <span className="text-muted small fw-semibold">Completed Consultations</span>
                            <div className="rounded-circle bg-success-subtle text-success p-3">
                                <FaCheckCircle size={20} />
                            </div>
                        </div>
                        <h3 className="fw-bold text-success mb-0 font-playfair">
                            {appointments.filter(a => a.status === 'Completed').length}
                        </h3>
                        <small className="text-muted">Treated patients</small>
                    </div>
                </div>
            </div>

            {/* Appointments Table */}
            <div className="card border-0 shadow-sm rounded-4 p-4 bg-white">
                <div className="d-flex justify-content-between align-items-center mb-4">
                    <div>
                        <h4 className="fw-bold font-playfair text-dark mb-1">
                            Patient Appointments {doctor?.doctorName ? `for Dr. ${doctor.doctorName}` : ''}
                        </h4>
                        <p className="text-muted small mb-0">Manage scheduled pulse examinations, video consultations, and health advice</p>
                    </div>
                    <button
                        onClick={loadAppointments}
                        className="btn btn-outline-secondary rounded-pill btn-sm px-3"
                    >
                        Refresh List
                    </button>
                </div>

                {loadingAppointments ? (
                    <div className="py-4 text-center">
                        <div className="spinner-border text-success" role="status"></div>
                    </div>
                ) : appointments.length === 0 ? (
                    <div className="text-center py-5 bg-light rounded-4 p-4">
                        <FaCalendarCheck size={40} className="text-muted mb-2" />
                        <h6 className="fw-semibold text-secondary">No appointment bookings yet</h6>
                        <p className="text-muted small mb-0">When patients book consultations through your profile, they will appear here.</p>
                    </div>
                ) : (
                    <div className="table-responsive">
                        <table className="table align-middle">
                            <thead className="table-light small text-secondary">
                                <tr>
                                    <th>Patient</th>
                                    <th>Contact</th>
                                    <th>Schedule</th>
                                    <th>Concern / Health Issue</th>
                                    <th>Status</th>
                                    <th className="text-end">Actions</th>
                                </tr>
                            </thead>
                            <tbody>
                                {appointments.map((apt) => (
                                    <tr key={apt.id}>
                                        <td>
                                            <div className="fw-bold text-dark">{apt.patientName}</div>
                                            <small className="text-muted">{apt.patientEmail}</small>
                                        </td>
                                        <td>
                                            <div className="small text-secondary">{apt.patientContact}</div>
                                        </td>
                                        <td>
                                            <div className="small fw-semibold text-dark">
                                                {new Date(apt.appointmentDate).toLocaleDateString()}
                                            </div>
                                            <small className="text-muted">{apt.timeSlot}</small>
                                        </td>
                                        <td>
                                            <div className="small text-muted" style={{ maxWidth: '240px' }}>
                                                {apt.healthConcern || apt.problemDescription || "Routine pulse consultation"}
                                            </div>
                                        </td>
                                        <td>
                                            <span className={`badge rounded-pill px-3 py-1 ${
                                                apt.status === 'Confirmed' ? 'bg-success-subtle text-success' :
                                                apt.status === 'Completed' ? 'bg-primary-subtle text-primary' :
                                                apt.status === 'Cancelled' ? 'bg-danger-subtle text-danger' :
                                                'bg-warning-subtle text-dark'
                                            }`}>
                                                {apt.status}
                                            </span>
                                        </td>
                                        <td className="text-end">
                                            {apt.status === 'Pending' && (
                                                <div className="d-flex justify-content-end gap-1">
                                                    <button
                                                        onClick={() => handleUpdateAppointmentStatus(apt.id, 'Confirmed')}
                                                        className="btn btn-sm btn-success rounded-pill px-2 py-0 small"
                                                        title="Confirm consultation"
                                                    >
                                                        Confirm
                                                    </button>
                                                    <button
                                                        onClick={() => handleUpdateAppointmentStatus(apt.id, 'Cancelled')}
                                                        className="btn btn-sm btn-outline-danger rounded-pill px-2 py-0 small"
                                                        title="Cancel request"
                                                    >
                                                        Decline
                                                    </button>
                                                </div>
                                            )}
                                            {apt.status === 'Confirmed' && (
                                                <button
                                                    onClick={() => handleUpdateAppointmentStatus(apt.id, 'Completed')}
                                                    className="btn btn-sm btn-primary-ayur text-white rounded-pill px-3 py-0 small"
                                                >
                                                    Mark Done
                                                </button>
                                            )}
                                            {apt.status === 'Completed' && (
                                                <small className="text-success fw-semibold">Completed</small>
                                            )}
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                )}
            </div>
        </div>
    );
}

export default Home;