import axios from "axios";
import { useRef, useState } from "react";
import Api from "../WebApi/Api";
import { toast } from "react-toastify";
import Header from "../Header";
import { useNavigate } from "react-router-dom";

function AdminAccount() {
    const emailSignInInput = useRef();
    const passwordSignInInput = useRef();
    const [emailSignInError, setEmailSignInError] = useState('');
    const [passwordSignInError, setPasswordSignInError] = useState('');
    const [loading, setLoading] = useState(false);
    const navigate = useNavigate();

    const handleSignIn = async () => {
        try {
            if (signInValidation()) {
                setLoading(true);
                let email = emailSignInInput.current.value.trim();
                let password = passwordSignInInput.current.value;
                let response = await axios.post(Api.adminSignIn, { email, password });
                
                if (response.data.token) {
                    sessionStorage.setItem('token', response.data.token);
                    sessionStorage.setItem('admin-token', response.data.token);
                }
                sessionStorage.setItem('admin-details', JSON.stringify(response.data.admin));
                toast.success(response.data.message || "Admin signed in successfully");
                navigate('/admin-dashboard');
            }
        } catch (err) {
            console.error(err);
            const msg = err.response?.data?.message || err.response?.data?.error || "Invalid admin credentials";
            toast.error(msg);
        } finally {
            setLoading(false);
        }
    };

    function signInValidation() {
        return validateSignInEmail(emailSignInInput.current.value) && validateSignInPassword(passwordSignInInput.current.value);
    }

    function validateSignInEmail(email) {
        const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        if (!emailRegex.test(email)) {
            setEmailSignInError('Please enter a valid email address');
            return false;
        } else {
            setEmailSignInError('');
            return true;
        }
    }

    function validateSignInPassword(password) {
        if (!password || password.length < 6) {
            setPasswordSignInError('Password must be at least 6 characters long');
            return false;
        } else {
            setPasswordSignInError('');
            return true;
        }
    }

    return (
        <>
            <Header />
            <div className="container py-5">
                <div className="row justify-content-center">
                    <div className="col-12 col-md-8 col-lg-5">
                        <div className="card shadow-lg border-0 rounded-4 p-4 p-md-5">
                            <div className="text-center mb-4">
                                <span className="badge bg-success-subtle text-success px-3 py-2 rounded-pill mb-2 fw-semibold">
                                    Administration Portal
                                </span>
                                <h3 className="fw-bold text-dark font-playfair mb-1">Admin Sign In</h3>
                                <p className="text-muted small">Access management dashboard and system controls</p>
                            </div>

                            <div>
                                <div className="mb-3">
                                    <label className="form-label small fw-semibold text-secondary">Admin Email</label>
                                    <input
                                        type="email"
                                        onChange={(e) => validateSignInEmail(e.target.value)}
                                        ref={emailSignInInput}
                                        placeholder="admin@ayurveda.com"
                                        className="form-control form-control-lg rounded-3"
                                        defaultValue="admin@ayurveda.com"
                                    />
                                    {emailSignInError && <div className="text-danger small mt-1">{emailSignInError}</div>}
                                </div>

                                <div className="mb-4">
                                    <label className="form-label small fw-semibold text-secondary">Password</label>
                                    <input
                                        type="password"
                                        onChange={(e) => validateSignInPassword(e.target.value)}
                                        ref={passwordSignInInput}
                                        placeholder="••••••••"
                                        className="form-control form-control-lg rounded-3"
                                        defaultValue="Admin@123"
                                    />
                                    {passwordSignInError && <div className="text-danger small mt-1">{passwordSignInError}</div>}
                                </div>

                                <button
                                    className="btn btn-primary-ayur btn-lg w-100 rounded-3 text-white fw-semibold mb-3 shadow-sm"
                                    onClick={handleSignIn}
                                    disabled={loading}
                                >
                                    {loading ? (
                                        <span>
                                            <span className="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>
                                            Signing In...
                                        </span>
                                    ) : (
                                        "Sign In As Admin"
                                    )}
                                </button>

                                <div className="text-center mt-3">
                                    <span className="text-muted small">
                                        Need practitioner access?{" "}
                                        <span
                                            onClick={() => navigate('/doctor-signIn')}
                                            className="text-success fw-semibold"
                                            style={{ cursor: 'pointer' }}
                                        >
                                            Doctor Portal
                                        </span>
                                    </span>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </>
    );
}

export default AdminAccount;