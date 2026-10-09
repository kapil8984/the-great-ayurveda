import axios from "axios";
import { useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import Api from "../WebApi/Api";
import { toast } from "react-toastify";
import Header from "../Header";

function UserAccount() {
    const navigate = useNavigate();
    const [isSignIn, setIsSignIn] = useState(true);
    const [loading, setLoading] = useState(false);

    // Errors
    const [nameError, setNameError] = useState('');
    const [emailError, setEmailError] = useState('');
    const [passwordError, setPasswordError] = useState('');
    const [mobileError, setMobileError] = useState('');
    const [emailSignInError, setEmailSignInError] = useState('');
    const [passwordSignInError, setPasswordSignInError] = useState('');

    // Refs
    const emailSignInInput = useRef();
    const passwordSignInInput = useRef();
    const nameInput = useRef();
    const passwordInput = useRef();
    const emailInput = useRef();
    const contactInput = useRef();

    function validateName(name) {
        if (!name || name.trim().length === 0) {
            setNameError('Please enter your full name');
            return false;
        } else {
            setNameError('');
            return true;
        }
    }

    function validateEmail(email) {
        const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        if (!emailRegex.test(email)) {
            setEmailError('Please enter a valid email');
            return false;
        } else {
            setEmailError('');
            return true;
        }
    }

    function validatePassword(password) {
        if (!password || password.length < 6) {
            setPasswordError('Password must be at least 6 characters long');
            return false;
        } else {
            setPasswordError('');
            return true;
        }
    }

    function validateContact(contact) {
        const contactRegex = /^\d{10}$/;
        if (!contactRegex.test(contact)) {
            setMobileError('Please enter a 10-digit mobile number');
            return false;
        } else {
            setMobileError('');
            return true;
        }
    }

    function validateSignInEmail(email) {
        const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        if (!emailRegex.test(email)) {
            setEmailSignInError('Please enter a valid email');
            return false;
        } else {
            setEmailSignInError('');
            return true;
        }
    }

    function validateSignInPassword(password) {
        if (!password || password.length < 6) {
            setPasswordSignInError('Password must be at least 6 characters');
            return false;
        } else {
            setPasswordSignInError('');
            return true;
        }
    }

    const handleSignUp = async (e) => {
        if (e) e.preventDefault();
        try {
            const isValid = validateName(nameInput.current?.value) &&
                            validateEmail(emailInput.current?.value) &&
                            validatePassword(passwordInput.current?.value) &&
                            validateContact(contactInput.current?.value);
            if (isValid) {
                setLoading(true);
                let username = nameInput.current.value.trim();
                let email = emailInput.current.value.trim();
                let password = passwordInput.current.value;
                let contact = contactInput.current.value.trim();
                
                let response = await axios.post(Api.userSignUp, { username, email, password, contact });
                toast.success(response.data.message || "Registration successful! Please sign in.");
                setIsSignIn(true);
            }
        } catch (err) {
            console.error(err);
            const msg = err.response?.data?.message || err.response?.data?.error || "Registration failed";
            toast.error(msg);
        } finally {
            setLoading(false);
        }
    };

    const handleSignIn = async (e) => {
        if (e) e.preventDefault();
        try {
            const isValid = validateSignInEmail(emailSignInInput.current?.value) &&
                            validateSignInPassword(passwordSignInInput.current?.value);
            if (isValid) {
                setLoading(true);
                let email = emailSignInInput.current.value.trim();
                let password = passwordSignInInput.current.value;
                let response = await axios.post(Api.userSignIn, { email, password });
                
                if (response.data.token) {
                    sessionStorage.setItem('token', response.data.token);
                    sessionStorage.setItem('user-token', response.data.token);
                }
                sessionStorage.setItem('user-details', JSON.stringify(response.data.user));
                toast.success(response.data.message || "Welcome back!");
                navigate('/');
            }
        } catch (err) {
            console.error(err);
            const msg = err.response?.data?.message || err.response?.data?.error || "Invalid email or password";
            toast.error(msg);
        } finally {
            setLoading(false);
        }
    };

    return (
        <>
            <Header />
            <div className="container py-5">
                <div className="row justify-content-center">
                    <div className="col-12 col-md-8 col-lg-5">
                        <div className="card shadow-lg border-0 rounded-4 p-4 p-md-5">
                            {/* Toggle Header */}
                            <div className="d-flex justify-content-center gap-3 mb-4 pb-2 border-bottom">
                                <button
                                    type="button"
                                    onClick={() => setIsSignIn(true)}
                                    className={`btn btn-link text-decoration-none fw-bold fs-5 px-3 py-1 ${isSignIn ? 'text-success border-bottom border-3 border-success' : 'text-muted'}`}
                                >
                                    Sign In
                                </button>
                                <button
                                    type="button"
                                    onClick={() => setIsSignIn(false)}
                                    className={`btn btn-link text-decoration-none fw-bold fs-5 px-3 py-1 ${!isSignIn ? 'text-success border-bottom border-3 border-success' : 'text-muted'}`}
                                >
                                    Sign Up
                                </button>
                            </div>

                            {/* Sign In Form */}
                            {isSignIn ? (
                                <form onSubmit={handleSignIn}>
                                    <div className="text-center mb-3">
                                        <h4 className="fw-bold font-playfair text-dark">Welcome to Ayurveda</h4>
                                        <p className="text-muted small">Sign in to manage your wellness journey</p>
                                    </div>

                                    <div className="mb-3">
                                        <label className="form-label small fw-semibold text-secondary">Email Address</label>
                                        <input
                                            type="email"
                                            onChange={(e) => validateSignInEmail(e.target.value)}
                                            ref={emailSignInInput}
                                            placeholder="user@ayurveda.com"
                                            className="form-control form-control-lg rounded-3"
                                            defaultValue="user@ayurveda.com"
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
                                            defaultValue="User@123"
                                        />
                                        {passwordSignInError && <div className="text-danger small mt-1">{passwordSignInError}</div>}
                                    </div>

                                    <button
                                        type="submit"
                                        className="btn btn-primary-ayur btn-lg w-100 rounded-3 text-white fw-semibold mb-3 shadow-sm"
                                        disabled={loading}
                                    >
                                        {loading ? (
                                            <span>
                                                <span className="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>
                                                Signing in...
                                            </span>
                                        ) : "Sign In As User"}
                                    </button>

                                    <div className="text-center mt-3 pt-2 border-top">
                                        <span className="text-muted small">
                                            Are you an Ayurvedic Practitioner?{" "}
                                            <span
                                                onClick={() => navigate('/doctor-signIn')}
                                                className="text-success fw-semibold"
                                                style={{ cursor: 'pointer' }}
                                            >
                                                Doctor Portal
                                            </span>
                                        </span>
                                    </div>
                                </form>
                            ) : (
                                /* Sign Up Form */
                                <form onSubmit={handleSignUp}>
                                    <div className="text-center mb-3">
                                        <h4 className="fw-bold font-playfair text-dark">Create Account</h4>
                                        <p className="text-muted small">Join our community for natural healing</p>
                                    </div>

                                    <div className="mb-3">
                                        <label className="form-label small fw-semibold text-secondary">Full Name</label>
                                        <input
                                            type="text"
                                            onChange={(e) => validateName(e.target.value)}
                                            ref={nameInput}
                                            placeholder="Aarav Sharma"
                                            className="form-control rounded-3"
                                        />
                                        {nameError && <div className="text-danger small mt-1">{nameError}</div>}
                                    </div>

                                    <div className="mb-3">
                                        <label className="form-label small fw-semibold text-secondary">Email Address</label>
                                        <input
                                            type="email"
                                            onChange={(e) => validateEmail(e.target.value)}
                                            ref={emailInput}
                                            placeholder="name@example.com"
                                            className="form-control rounded-3"
                                        />
                                        {emailError && <div className="text-danger small mt-1">{emailError}</div>}
                                    </div>

                                    <div className="mb-3">
                                        <label className="form-label small fw-semibold text-secondary">Password</label>
                                        <input
                                            type="password"
                                            onChange={(e) => validatePassword(e.target.value)}
                                            ref={passwordInput}
                                            placeholder="At least 6 characters"
                                            className="form-control rounded-3"
                                        />
                                        {passwordError && <div className="text-danger small mt-1">{passwordError}</div>}
                                    </div>

                                    <div className="mb-4">
                                        <label className="form-label small fw-semibold text-secondary">Mobile Number (10 digits)</label>
                                        <input
                                            type="tel"
                                            onChange={(e) => validateContact(e.target.value)}
                                            ref={contactInput}
                                            placeholder="9876543210"
                                            className="form-control rounded-3"
                                        />
                                        {mobileError && <div className="text-danger small mt-1">{mobileError}</div>}
                                    </div>

                                    <button
                                        type="submit"
                                        className="btn btn-primary-ayur btn-lg w-100 rounded-3 text-white fw-semibold mb-3 shadow-sm"
                                        disabled={loading}
                                    >
                                        {loading ? (
                                            <span>
                                                <span className="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>
                                                Creating Account...
                                            </span>
                                        ) : "Create Account"}
                                    </button>

                                    <div className="text-center mt-2">
                                        <span className="text-muted small">
                                            Already registered?{" "}
                                            <span
                                                onClick={() => setIsSignIn(true)}
                                                className="text-success fw-semibold"
                                                style={{ cursor: 'pointer' }}
                                            >
                                                Sign In
                                            </span>
                                        </span>
                                    </div>
                                </form>
                            )}
                        </div>
                    </div>
                </div>
            </div>
        </>
    );
}

export default UserAccount;