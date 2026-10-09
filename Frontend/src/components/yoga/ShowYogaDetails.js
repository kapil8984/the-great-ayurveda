import React, { useEffect, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import starGenerate from "../product/GenerateRating";
import axios from "axios";
import Api from "../WebApi/Api";
import { toast } from "react-toastify";
import { FaCheckCircle, FaArrowLeft } from "react-icons/fa";

function ShowYogaDetails() {
    const location = useLocation();
    const navigate = useNavigate();
    const yoga = location.state?.yoga;

    const [currentUser, setCurrentUser] = useState(null);
    const [currentStar, setCurrentStar] = useState(5);
    const [review, setReview] = useState('');
    const [userReview, setUserReview] = useState(null);
    const [reviews, setReviews] = useState([]);
    const [submitting, setSubmitting] = useState(false);

    function getYouTubeVideoId(url) {
        if (!url) return '';
        const match = url.match(/(?:https?:\/\/)?(?:www\.)?youtube\.com\/watch\?v=([a-zA-Z0-9_-]{11})/) ||
                      url.match(/(?:https?:\/\/)?(?:www\.)?youtu\.be\/([a-zA-Z0-9_-]{11})/);
        return match && match[1] ? match[1] : '';
    }

    useEffect(() => {
        if (!yoga) {
            navigate('/yoga');
            return;
        }
        const user = JSON.parse(sessionStorage.getItem('user-details') || 'null');
        setCurrentUser(user);
        loadReviewsAndRating(user);
        window.scrollTo(0, 0);
    }, [yoga, navigate]);

    const loadReviewsAndRating = async (user) => {
        if (!yoga) return;
        const yogaId = yoga._id || yoga.id;
        try {
            const allRes = await axios.get(`${Api.getReviewForYoga}/${yogaId}`);
            setReviews(allRes.data || []);

            if (user) {
                const userId = user._id || user.id;
                try {
                    const userRes = await axios.get(`${Api.getReviewForYogaByUser}/${yogaId}/${userId}`);
                    if (userRes.data && userRes.data.rating) {
                        setUserReview(userRes.data);
                        setCurrentStar(userRes.data.rating);
                        setReview(userRes.data.review || "");
                    }
                } catch (e) {
                    // No existing review by this user
                }
            }
        } catch (error) {
            console.error("Reviews load error:", error);
        }
    };

    const handleSaveReview = async (e) => {
        e.preventDefault();
        if (!currentUser) {
            toast.info("Please sign in to rate this yoga asana");
            navigate('/user-account');
            return;
        }

        if (!review.trim()) {
            toast.warning("Please share your feedback or experience with this posture");
            return;
        }

        try {
            setSubmitting(true);
            const yogaId = yoga._id || yoga.id;
            const userId = currentUser._id || currentUser.id;

            const payload = {
                userId,
                yogaId,
                rating: currentStar,
                review: review.trim()
            };

            if (userReview) {
                await axios.put(Api.updateYogaReviewAndRating, payload);
                toast.success("Review updated successfully!");
            } else {
                await axios.post(Api.addRateAndReviewUserYoga, payload);
                toast.success("Thank you! Review published.");
            }

            loadReviewsAndRating(currentUser);
        } catch (error) {
            console.error("Review save error:", error);
            toast.error("Could not save review");
        } finally {
            setSubmitting(false);
        }
    };

    if (!yoga) return null;

    const videoId = getYouTubeVideoId(yoga.videoUrl);
    const benefitsList = Array.isArray(yoga.benefits)
        ? yoga.benefits
        : (typeof yoga.benefits === 'string' ? yoga.benefits.split(',').map(b => b.trim()).filter(Boolean) : []);

    return (
        <div className="container py-5">
            <button
                onClick={() => navigate('/yoga')}
                className="btn btn-link text-success text-decoration-none p-0 mb-4 d-inline-flex align-items-center gap-2 fw-semibold"
            >
                <FaArrowLeft /> Back to Yoga Directory
            </button>

            {/* Asana Showcase Card */}
            <div className="card border-0 shadow-sm rounded-4 p-4 p-md-5 mb-5 bg-white">
                <div className="row g-5">
                    {/* Left: Video Tutorial Embed */}
                    <div className="col-12 col-lg-6">
                        <div className="rounded-4 overflow-hidden shadow-sm bg-black ratio ratio-16x9">
                            {videoId ? (
                                <iframe
                                    src={`https://www.youtube.com/embed/${videoId}?rel=0`}
                                    title={yoga.yogaName}
                                    allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
                                    allowFullScreen
                                ></iframe>
                            ) : (
                                <img
                                    src={yoga.imageUrl || 'https://i.ibb.co/kSn83hn/hinduist-yoga-position.png'}
                                    alt={yoga.yogaName}
                                    className="img-fluid object-fit-cover w-100 h-100"
                                />
                            )}
                        </div>
                    </div>

                    {/* Right: Overview, Benefits, Instructions */}
                    <div className="col-12 col-lg-6">
                        <span className="badge bg-success-subtle text-success rounded-pill px-3 py-1 fw-semibold small mb-2">
                            Guided Asana
                        </span>
                        <h2 className="fw-bold font-playfair text-dark mb-2">{yoga.yogaName}</h2>

                        <div className="mb-3">
                            {starGenerate(yoga.averageRating || 5)}
                        </div>

                        <div className="mb-4">
                            <h6 className="fw-bold text-dark font-playfair">Therapeutic Overview</h6>
                            <p className="text-secondary small leading-relaxed">
                                {yoga.Description || yoga.description}
                            </p>
                        </div>

                        {benefitsList.length > 0 && (
                            <div className="mb-4">
                                <h6 className="fw-bold text-dark font-playfair">Key Health Benefits</h6>
                                <ul className="list-unstyled mb-0 small text-secondary">
                                    {benefitsList.map((b, bIdx) => (
                                        <li key={bIdx} className="mb-1 d-flex align-items-baseline gap-2">
                                            <FaCheckCircle className="text-success flex-shrink-0" size={12} />
                                            <span>{b}</span>
                                        </li>
                                    ))}
                                </ul>
                            </div>
                        )}

                        {yoga.instructions && (
                            <div>
                                <h6 className="fw-bold text-dark font-playfair">Step-by-Step Breathing & Posture Instructions</h6>
                                <p className="text-secondary small bg-light p-3 rounded-3 leading-relaxed mb-0">
                                    {yoga.instructions}
                                </p>
                            </div>
                        )}
                    </div>
                </div>
            </div>

            {/* Ratings and Reviews Section */}
            <div className="card border-0 shadow-sm rounded-4 p-4 p-md-5 bg-white">
                <h4 className="fw-bold font-playfair text-dark mb-4">Practitioner Experiences & Reviews</h4>

                <div className="row g-5">
                    {/* Left: Reviews List */}
                    <div className="col-12 col-md-7">
                        {reviews.length === 0 ? (
                            <div className="text-center py-4 bg-light rounded-4 p-4">
                                <p className="text-muted mb-0 small">No reviews submitted yet. Practice this asana and share your experience!</p>
                            </div>
                        ) : (
                            <div className="space-y-3">
                                {reviews.map((rev, rIdx) => (
                                    <div key={rIdx} className="border-bottom pb-3 mb-3">
                                        <div className="d-flex justify-content-between align-items-center mb-1">
                                            <strong className="text-dark small">
                                                {rev.user?.username || rev.userName || "Yoga Practitioner"}
                                            </strong>
                                            <div>{starGenerate(rev.rating || 5)}</div>
                                        </div>
                                        <p className="text-secondary small mb-0">{rev.review}</p>
                                    </div>
                                ))}
                            </div>
                        )}
                    </div>

                    {/* Right: Review Form */}
                    <div className="col-12 col-md-5">
                        <div className="bg-light p-4 rounded-4">
                            <h5 className="fw-bold text-dark font-playfair mb-3">
                                {userReview ? "Update Your Experience" : "Share Your Experience"}
                            </h5>
                            <form onSubmit={handleSaveReview}>
                                <div className="mb-3">
                                    <label className="form-label small fw-semibold text-secondary">Your Rating</label>
                                    <div className="d-flex gap-2">
                                        {[1, 2, 3, 4, 5].map((star) => (
                                            <span
                                                key={star}
                                                onClick={() => setCurrentStar(star)}
                                                style={{ cursor: 'pointer', fontSize: '26px', color: star <= currentStar ? '#ffc107' : '#e0e0e0' }}
                                            >
                                                ★
                                            </span>
                                        ))}
                                    </div>
                                </div>

                                <div className="mb-3">
                                    <label className="form-label small fw-semibold text-secondary">Comments & Benefits Noticed</label>
                                    <textarea
                                        className="form-control rounded-3"
                                        rows="4"
                                        value={review}
                                        onChange={(e) => setReview(e.target.value)}
                                        placeholder="How did this posture feel? Did it relieve tension or improve breathing?"
                                        required
                                    ></textarea>
                                </div>

                                <button
                                    type="submit"
                                    className="btn btn-primary-ayur text-white rounded-pill px-4 fw-semibold w-100"
                                    disabled={submitting}
                                >
                                    {submitting ? "Saving..." : (userReview ? "Update Review" : "Submit Review")}
                                </button>
                            </form>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
}

export default ShowYogaDetails;
