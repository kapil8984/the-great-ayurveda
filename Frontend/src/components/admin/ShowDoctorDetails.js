import React, { useEffect, useState } from "react";
import { useSelector, useDispatch } from "react-redux";
import Api from "../WebApi/Api";
import axios from "axios";
import { toast } from "react-toastify";
import { useNavigate } from "react-router-dom";
import { getPendingDoctor } from "../../redux-config/PendinDoctorSlice";
import { FaArrowLeft, FaCheckCircle, FaTimesCircle, FaGraduationCap } from "react-icons/fa";

function ShowDoctorDetails() {
    const navigate = useNavigate();
    const dispatch = useDispatch();
    const { pendingDoctorList, current } = useSelector((store) => store.pendingDoctor);
    const [doctor, setDoctor] = useState(null);
    const [loadingAction, setLoadingAction] = useState(false);

    useEffect(() => {
        if (pendingDoctorList && pendingDoctorList[current]) {
            setDoctor(pendingDoctorList[current]);
        } else {
            navigate('/admin-dashboard/pending-doctor-request');
        }
    }, [pendingDoctorList, current, navigate]);

    const handleDoctorApproveAccept = async () => {
        try {
            setLoadingAction(true);
            const docId = doctor.doctor?._id || doctor.doctor?.id;
            const response = await axios.put(Api.approveDoctorRequestAccept, { doctorId: docId });
            toast.success(response.data.message || "Doctor approved successfully!");
            dispatch(getPendingDoctor());
            navigate('/admin-dashboard/pending-doctor-request');
        } catch (error) {
            console.error(error);
            toast.error(error.response?.data?.error || "Approval failed");
        } finally {
            setLoadingAction(false);
        }
    };

    const handleDoctorApproveReject = async () => {
        if (!window.confirm("Are you sure you want to reject this doctor application?")) return;
        try {
            setLoadingAction(true);
            const docId = doctor.doctor?._id || doctor.doctor?.id;
            const response = await axios.put(Api.approveDoctorRequestReject, { doctorId: docId });
            toast.info(response.data.message || "Doctor application rejected");
            dispatch(getPendingDoctor());
            navigate('/admin-dashboard/pending-doctor-request');
        } catch (error) {
            console.error(error);
            toast.error(error.response?.data?.error || "Rejection failed");
        } finally {
            setLoadingAction(false);
        }
    };

    if (!doctor) return null;

    const doc = doctor.doctor || {};
    const details = doctor.doctorDetails || {};
    const qualImg = details.qualificationImage ? `${Api.imageBaseUrl}${details.qualificationImage}` : null;
    const profileImg = doc.profileImage ? `${Api.imageBaseUrl}${doc.profileImage}` : null;

    return (
        <div className="container-fluid py-3">
            <button
                onClick={() => navigate('/admin-dashboard/pending-doctor-request')}
                className="btn btn-link text-success text-decoration-none p-0 mb-4 d-inline-flex align-items-center gap-2 fw-semibold"
            >
                <FaArrowLeft /> Back to Pending Applications
            </button>

            <div className="row g-4">
                {/* Left Column: Personal and Professional Profile */}
                <div className="col-12 col-lg-5">
                    <div className="card border-0 shadow-sm rounded-4 p-4 p-md-5 bg-white h-100">
                        <div className="text-center mb-4">
                            {profileImg ? (
                                <img
                                    src={profileImg}
                                    alt={doc.doctorName}
                                    className="rounded-circle border border-3 border-success object-fit-cover shadow mx-auto mb-3"
                                    style={{ width: '120px', height: '120px' }}
                                    onError={(e) => { e.target.onerror = null; e.target.src = 'https://cdn-icons-png.flaticon.com/512/3774/3774299.png'; }}
                                />
                            ) : (
                                <div className="rounded-circle bg-success text-white d-flex align-items-center justify-content-center shadow mx-auto mb-3 fw-bold fs-2" style={{ width: '120px', height: '120px' }}>
                                    {doc.doctorName ? doc.doctorName.charAt(0) : 'D'}
                                </div>
                            )}

                            <h4 className="fw-bold font-playfair text-dark mb-1">{doc.doctorName}</h4>
                            <span className="badge bg-success-subtle text-success rounded-pill px-3 py-1 fw-semibold small">
                                {details.specialization || "Ayurvedic Medicine"}
                            </span>
                        </div>

                        <div className="space-y-3 mb-4 small text-secondary">
                            <div className="d-flex justify-content-between border-bottom pb-2">
                                <strong>Email Address:</strong>
                                <span>{doc.email}</span>
                            </div>
                            <div className="d-flex justify-content-between border-bottom pb-2">
                                <strong>Contact Number:</strong>
                                <span>{doc.contactNumber || "N/A"}</span>
                            </div>
                            <div className="d-flex justify-content-between border-bottom pb-2">
                                <strong>Experience:</strong>
                                <span>{details.experience ? `${details.experience} Years` : 'N/A'}</span>
                            </div>
                            <div className="d-flex justify-content-between border-bottom pb-2">
                                <strong>Languages Spoken:</strong>
                                <span>{details.language || "English, Hindi"}</span>
                            </div>
                            <div className="d-flex justify-content-between border-bottom pb-2">
                                <strong>Clinic Address:</strong>
                                <span>{details.clinicAddress || "N/A"}</span>
                            </div>
                            <div className="d-flex justify-content-between">
                                <strong>Timings:</strong>
                                <span>{details.timing || "Standard"}</span>
                            </div>
                        </div>

                        <div className="d-flex gap-2 mt-auto pt-3 border-top">
                            <button
                                onClick={handleDoctorApproveAccept}
                                className="btn btn-success rounded-pill w-100 fw-semibold d-flex align-items-center justify-content-center gap-2"
                                disabled={loadingAction}
                            >
                                <FaCheckCircle /> Approve Doctor
                            </button>
                            <button
                                onClick={handleDoctorApproveReject}
                                className="btn btn-outline-danger rounded-pill w-100 fw-semibold d-flex align-items-center justify-content-center gap-2"
                                disabled={loadingAction}
                            >
                                <FaTimesCircle /> Reject
                            </button>
                        </div>
                    </div>
                </div>

                {/* Right Column: Qualification Document / License Certificate */}
                <div className="col-12 col-lg-7">
                    <div className="card border-0 shadow-sm rounded-4 p-4 p-md-5 bg-white h-100">
                        <div className="d-flex align-items-center gap-2 mb-3">
                            <FaGraduationCap className="text-success" size={24} />
                            <h5 className="fw-bold font-playfair text-dark mb-0">Ayurvedic Degree & Registration Certificate</h5>
                        </div>
                        <p className="text-muted small mb-4">
                            Examine the practitioner's BAMS/MD registration certificate to verify authenticity against State Ayurvedic Council records.
                        </p>

                        <div className="text-center bg-light rounded-4 p-3 border d-flex align-items-center justify-content-center flex-grow-1" style={{ minHeight: '380px' }}>
                            {qualImg ? (
                                <img
                                    src={qualImg}
                                    alt="Degree Certificate"
                                    className="img-fluid rounded-3 shadow-sm object-fit-contain"
                                    style={{ maxHeight: '500px', width: '100%' }}
                                    onError={(e) => { e.target.onerror = null; e.target.src = 'https://images.unsplash.com/photo-1589829545856-d10d557cf95f?auto=format&fit=crop&w=800&q=80'; }}
                                />
                            ) : (
                                <div className="p-5 text-muted">
                                    <FaGraduationCap size={48} className="mb-2 text-secondary" />
                                    <p className="mb-0">No qualification image uploaded by practitioner.</p>
                                </div>
                            )}
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
}

export default ShowDoctorDetails;