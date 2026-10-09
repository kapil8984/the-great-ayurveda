import React, { useEffect, useState } from "react";
import { Outlet, useNavigate } from "react-router-dom";
import axios from 'axios';
import Api from "../WebApi/Api";
import { toast } from "react-toastify";
import { FaUserMd, FaCamera, FaEnvelope, FaPhone, FaKey, FaEdit, FaSignOutAlt, FaPlusCircle } from "react-icons/fa";

function DoctorProfile() {
    const [doctor, setDoctor] = useState({});
    const [doctorDetails, setDoctorDetails] = useState({});
    const navigate = useNavigate();

    useEffect(() => {
        const storedDoc = JSON.parse(sessionStorage.getItem('doctor-details') || 'null');
        if (!storedDoc) {
            navigate('/doctor-signIn');
            return;
        }
        setDoctor(storedDoc);
        const storedDetails = JSON.parse(sessionStorage.getItem('doctor-extra-details') || 'null');
        setDoctorDetails(storedDetails || {});
    }, [navigate]);

    const handleLogOut = () => {
        sessionStorage.removeItem('doctor-details');
        sessionStorage.removeItem('doctor-extra-details');
        sessionStorage.removeItem('token');
        sessionStorage.removeItem('doctor-token');
        toast.info("Doctor signed out");
        navigate('/');
    };

    const handleFileInputChange = (event) => {
        const file = event.target.files[0];
        if (!file) return;

        const formData = new FormData();
        const doctorId = doctor._id || doctor.id;
        formData.append('doctorId', doctorId);
        formData.append('profile', file);
        formData.append('doctor', JSON.stringify(doctor));

        axios.post(Api.addProfilePicture, formData, {
            headers: {
                'Content-Type': 'multipart/form-data'
            }
        })
            .then(response => {
                const updatedDoctor = { ...doctor, profileImage: response.data.filePath };
                setDoctor(updatedDoctor);
                sessionStorage.setItem('doctor-details', JSON.stringify(updatedDoctor));
                toast.success('Profile picture updated successfully');
            })
            .catch(error => {
                console.error('Error uploading file:', error);
                toast.error('Could not upload profile picture');
            });
    };

    const profileImg = doctor.profileImage ? `${Api.imageBaseUrl}${doctor.profileImage}` : null;

    return (
        <div className="container-fluid p-4">
            <div className="card border-0 shadow-sm rounded-4 p-4 p-md-5 bg-white mb-4">
                <div className="row g-4 align-items-center">
                    <div className="col-12 col-md-4 text-center">
                        <div className="position-relative d-inline-block">
                            {profileImg ? (
                                <img
                                    src={profileImg}
                                    alt={doctor.doctorName}
                                    className="rounded-circle border border-3 border-success object-fit-cover shadow"
                                    style={{ width: '160px', height: '160px' }}
                                    onError={(e) => { e.target.onerror = null; e.target.src = 'https://cdn-icons-png.flaticon.com/512/3774/3774299.png'; }}
                                />
                            ) : (
                                <div className="rounded-circle bg-success text-white d-flex align-items-center justify-content-center shadow mx-auto fw-bold fs-1" style={{ width: '160px', height: '160px' }}>
                                    {doctor.doctorName ? doctor.doctorName.charAt(0) : 'D'}
                                </div>
                            )}

                            <label
                                htmlFor="fileInput"
                                className="btn btn-sm btn-success rounded-circle position-absolute bottom-0 end-0 p-2 shadow"
                                style={{ cursor: 'pointer' }}
                                title="Update profile picture"
                            >
                                <FaCamera size={16} />
                            </label>
                            <input type='file' id="fileInput" style={{ display: 'none' }} accept="image/*" onChange={handleFileInputChange} />
                        </div>
                    </div>

                    <div className="col-12 col-md-8">
                        <span className="badge bg-success-subtle text-success px-3 py-1 rounded-pill fw-semibold mb-2">
                            Practitioner Profile
                        </span>
                        <h3 className="fw-bold font-playfair text-dark mb-1">{doctor.doctorName}</h3>
                        <p className="text-muted small mb-3">Certified Ayurvedic Health Professional</p>

                        <div className="space-y-2 mb-4 text-secondary small">
                            <div className="d-flex align-items-center gap-2 mb-2">
                                <FaEnvelope className="text-success" />
                                <span><strong>Email:</strong> {doctor.email}</span>
                            </div>
                            <div className="d-flex align-items-center gap-2 mb-2">
                                <FaPhone className="text-success" />
                                <span><strong>Contact:</strong> {doctor.contactNumber || "N/A"}</span>
                            </div>
                            {doctorDetails?.specialization && (
                                <div className="d-flex align-items-center gap-2">
                                    <FaUserMd className="text-success" />
                                    <span><strong>Specialization:</strong> {doctorDetails.specialization} ({doctorDetails.experience || "0"} Yrs Experience)</span>
                                </div>
                            )}
                        </div>

                        <div className="d-flex flex-wrap gap-2">
                            <button
                                className="btn btn-sm btn-outline-success rounded-pill px-3 d-flex align-items-center gap-1"
                                onClick={() => navigate('/doctor-dashboard/doctor-profile/edit-profile', { state: { doctor } })}
                            >
                                <FaEdit /> Edit Profile
                            </button>
                            <button
                                className="btn btn-sm btn-outline-success rounded-pill px-3 d-flex align-items-center gap-1"
                                onClick={() => navigate('/doctor-dashboard/doctor-profile/change-password', { state: { doctor } })}
                            >
                                <FaKey /> Change Password
                            </button>
                            <button
                                className="btn btn-sm btn-primary-ayur text-white rounded-pill px-3 d-flex align-items-center gap-1"
                                onClick={() => navigate('/doctor-dashboard/doctor-profile/add-details-doctor', { state: { doctor } })}
                            >
                                <FaPlusCircle /> Add / Update Details
                            </button>
                            <button
                                className="btn btn-sm btn-outline-danger rounded-pill px-3 d-flex align-items-center gap-1"
                                onClick={handleLogOut}
                            >
                                <FaSignOutAlt /> Sign Out
                            </button>
                        </div>
                    </div>
                </div>
            </div>

            <Outlet />
        </div>
    );
}

export default DoctorProfile;
