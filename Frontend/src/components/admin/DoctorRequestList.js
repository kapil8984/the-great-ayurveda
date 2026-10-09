import React, { useEffect } from "react";
import axios from "axios";
import Api from "../WebApi/Api";
import { useNavigate } from "react-router-dom";
import { useDispatch, useSelector } from "react-redux";
import { getPendingDoctor, updateCurrent } from "../../redux-config/PendinDoctorSlice";
import { toast } from "react-toastify";
import { FaCheckCircle, FaTimesCircle, FaEye } from "react-icons/fa";

function DoctorRequestList() {
    const { isLoading, pendingDoctorList } = useSelector((store) => store.pendingDoctor);
    const navigate = useNavigate();
    const dispatch = useDispatch();

    useEffect(() => {
        dispatch(getPendingDoctor());
    }, [dispatch]);

    const showDetails = (index) => {
        dispatch(updateCurrent(index));
        navigate('/admin-dashboard/show-doctor-details');
    };

    const handleDoctorApproveReject = async (doctorId) => {
        if (!window.confirm("Are you sure you want to reject this doctor application?")) return;
        try {
            const response = await axios.put(Api.approveDoctorRequestReject, { doctorId });
            dispatch(getPendingDoctor());
            toast.info(response.data.message || "Application rejected");
        } catch (error) {
            console.error(error);
            toast.error(error.response?.data?.error || "Action failed");
        }
    };

    return (
        <div className="container-fluid py-3">
            <div className="d-flex justify-content-between align-items-center mb-4">
                <div>
                    <h3 className="fw-bold font-playfair text-dark mb-1">Pending Practitioner Applications</h3>
                    <p className="text-muted small mb-0">Verify credentials and qualifications before granting practitioner privileges</p>
                </div>
                <span className="badge bg-warning-subtle text-dark border px-3 py-2 rounded-pill fw-semibold">
                    {pendingDoctorList.length} Pending Approval(s)
                </span>
            </div>

            {isLoading ? (
                <div className="py-5 text-center">
                    <div className="spinner-border text-success" role="status"></div>
                </div>
            ) : pendingDoctorList.length === 0 ? (
                <div className="text-center py-5 card border-0 shadow-sm rounded-4 p-5 mx-auto bg-white" style={{ maxWidth: '500px' }}>
                    <FaCheckCircle size={48} className="text-success mx-auto mb-3" />
                    <h5 className="fw-semibold text-secondary font-playfair">All Caught Up!</h5>
                    <p className="text-muted small mb-0">There are no pending doctor registration requests at this time.</p>
                </div>
            ) : (
                <div className="row g-4">
                    {pendingDoctorList.map((item, index) => {
                        const doctor = item.doctor || {};
                        const details = item.doctorDetails || {};
                        const docId = doctor._id || doctor.id;
                        const docImg = doctor.profileImage ? `${Api.imageBaseUrl}${doctor.profileImage}` : null;

                        return (
                            <div className="col-12 col-md-6 col-xl-4" key={index}>
                                <div className="card h-100 border-0 shadow-sm rounded-4 p-4 card-ayur bg-white d-flex flex-column">
                                    <div className="d-flex align-items-center gap-3 mb-3">
                                        {docImg ? (
                                            <img
                                                src={docImg}
                                                alt={doctor.doctorName}
                                                className="rounded-circle border border-2 border-success object-fit-cover shadow-sm"
                                                style={{ width: '70px', height: '70px' }}
                                                onError={(e) => { e.target.onerror = null; e.target.src = 'https://cdn-icons-png.flaticon.com/512/3774/3774299.png'; }}
                                            />
                                        ) : (
                                            <div className="rounded-circle bg-success text-white d-flex align-items-center justify-content-center shadow-sm fw-bold fs-4" style={{ width: '70px', height: '70px' }}>
                                                {doctor.doctorName ? doctor.doctorName.charAt(0) : 'D'}
                                            </div>
                                        )}

                                        <div>
                                            <h5 className="fw-bold mb-1 text-dark font-playfair">{doctor.doctorName}</h5>
                                            <small className="text-muted d-block">{doctor.email}</small>
                                            <small className="text-muted">{doctor.contactNumber || "No phone"}</small>
                                        </div>
                                    </div>

                                    <div className="bg-light p-3 rounded-3 small mb-4 flex-grow-1 space-y-1">
                                        <div className="d-flex justify-content-between mb-1">
                                            <span className="text-muted">Specialization:</span>
                                            <span className="fw-semibold text-dark">{details.specialization || "Ayurveda General"}</span>
                                        </div>
                                        <div className="d-flex justify-content-between mb-1">
                                            <span className="text-muted">Experience:</span>
                                            <span className="fw-semibold text-dark">{details.experience ? `${details.experience} Years` : 'N/A'}</span>
                                        </div>
                                        <div className="d-flex justify-content-between">
                                            <span className="text-muted">Languages:</span>
                                            <span className="fw-semibold text-dark">{details.language || "English, Hindi"}</span>
                                        </div>
                                    </div>

                                    <div className="d-flex gap-2 mt-auto">
                                        <button
                                            className="btn btn-primary-ayur text-white rounded-pill btn-sm flex-grow-1 fw-semibold d-flex align-items-center justify-content-center gap-1"
                                            onClick={() => showDetails(index)}
                                        >
                                            <FaEye /> View Certificate
                                        </button>
                                        <button
                                            className="btn btn-outline-danger rounded-pill btn-sm px-3 fw-semibold d-flex align-items-center justify-content-center gap-1"
                                            onClick={() => handleDoctorApproveReject(docId)}
                                        >
                                            <FaTimesCircle /> Reject
                                        </button>
                                    </div>
                                </div>
                            </div>
                        );
                    })}
                </div>
            )}
        </div>
    );
}

export default DoctorRequestList;
