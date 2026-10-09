import React, { useEffect, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import axios from "axios";
import Api from "../WebApi/Api";
import { toast } from "react-toastify";
import starGenerate from "./GenerateRating";
import { FaShoppingBag, FaBolt, FaCheckCircle, FaTimesCircle, FaLeaf, FaTruck, FaShieldAlt } from "react-icons/fa";

function ShowProductDetails() {
    const location = useLocation();
    const navigate = useNavigate();

    const [product, setProduct] = useState(location.state?.product || null);
    const [reviews, setReviews] = useState([]);
    const [quantity, setQuantity] = useState(1);
    const [relatedProducts, setRelatedProducts] = useState([]);

    // Review form state
    const [newRating, setNewRating] = useState(5);
    const [newReviewText, setNewReviewText] = useState("");
    const [submittingReview, setSubmittingReview] = useState(false);

    const currentUser = JSON.parse(sessionStorage.getItem('user-details') || 'null');

    useEffect(() => {
        if (!location.state?.product) {
            navigate('/get-all-products');
            return;
        }
        const currentProd = location.state.product;
        setProduct(currentProd);
        loadReviews(currentProd._id || currentProd.id);
        loadRelatedProducts(currentProd);
        window.scrollTo(0, 0);
    }, [location.state]);

    const loadReviews = async (productId) => {
        try {
            const response = await axios.post(Api.getProductReviewAndRating, { productId });
            setReviews(response.data || []);
        } catch (error) {
            console.error("Failed to load reviews:", error);
        }
    };

    const loadRelatedProducts = async (currentProd) => {
        try {
            const res = await axios.get(Api.getAllProduct);
            const all = res.data.product || res.data.products || [];
            const related = all
                .filter(p => (p._id || p.id) !== (currentProd._id || currentProd.id))
                .slice(0, 4);
            setRelatedProducts(related);
        } catch (err) {
            console.error(err);
        }
    };

    const handleAddToCart = async () => {
        if (!currentUser) {
            toast.info("Please sign in to add products to your cart");
            navigate('/user-account');
            return;
        }

        if (product.stock <= 0) {
            toast.warning("This product is currently out of stock");
            return;
        }

        try {
            const prodId = product._id || product.id;
            const userId = currentUser._id || currentUser.id;
            const response = await axios.post(Api.addToCart, { 
                productId: prodId, 
                userId,
                quantity: quantity 
            });
            toast.success(response.data.message || `Added ${quantity} item(s) to cart!`);
        } catch (error) {
            console.error(error);
            toast.error("Could not add to cart");
        }
    };

    const handleBuyNow = async () => {
        if (!currentUser) {
            toast.info("Please sign in to continue");
            navigate('/user-account');
            return;
        }
        await handleAddToCart();
        navigate('/show-cart');
    };

    const handleAddReview = async (e) => {
        e.preventDefault();
        if (!currentUser) {
            toast.info("Please sign in to leave a review");
            navigate('/user-account');
            return;
        }

        if (!newReviewText.trim()) {
            toast.warning("Please enter your review comments");
            return;
        }

        try {
            setSubmittingReview(true);
            const prodId = product._id || product.id;
            const userId = currentUser._id || currentUser.id;
            const payload = {
                userId,
                productId: prodId,
                rating: newRating,
                review: newReviewText.trim()
            };

            await axios.post(Api.addRateAndReviewUser, payload);
            toast.success("Thank you! Your review has been added.");
            setNewReviewText("");
            loadReviews(prodId);
        } catch (error) {
            console.error("Review error:", error);
            toast.error("Could not submit review");
        } finally {
            setSubmittingReview(false);
        }
    };

    if (!product) return null;

    const imgSrc = product.imageURL
        ? `${Api.imageBaseUrl}${product.imageURL}`
        : 'https://i.ibb.co/ZNB6jDz/ayurvedic-bowl.png';
    const inStock = product.stock > 0;

    return (
        <div className="container py-5">
            {/* Breadcrumb */}
            <nav aria-label="breadcrumb" className="mb-4">
                <ol className="breadcrumb small">
                    <li className="breadcrumb-item cursor-pointer" onClick={() => navigate('/')}>Home</li>
                    <li className="breadcrumb-item cursor-pointer" onClick={() => navigate('/get-all-products')}>Herbal Pharmacy</li>
                    <li className="breadcrumb-item active text-truncate" aria-current="page">{product.title}</li>
                </ol>
            </nav>

            {/* Product Overview Card */}
            <div className="card border-0 shadow-sm rounded-4 p-4 p-md-5 mb-5 bg-white">
                <div className="row g-5 align-items-center">
                    {/* Left: Product Image */}
                    <div className="col-12 col-md-5 text-center">
                        <div className="p-4 bg-light rounded-4 d-inline-block w-100 shadow-inner">
                            <img
                                src={imgSrc}
                                alt={product.title}
                                className="img-fluid object-fit-contain"
                                style={{ maxHeight: '380px' }}
                                onError={(e) => { e.target.onerror = null; e.target.src = 'https://i.ibb.co/ZNB6jDz/ayurvedic-bowl.png'; }}
                            />
                        </div>
                    </div>

                    {/* Right: Product Details */}
                    <div className="col-12 col-md-7">
                        <div className="d-flex align-items-center gap-2 mb-2">
                            <span className="badge bg-success-subtle text-success rounded-pill px-3 py-1 fw-semibold small">
                                {product.brand || "Classical Ayurveda"}
                            </span>
                            {product.category && (
                                <span className="badge bg-secondary-subtle text-secondary rounded-pill px-3 py-1 small">
                                    {product.category.name}
                                </span>
                            )}
                        </div>

                        <h2 className="fw-bold font-playfair text-dark mb-2">{product.title}</h2>

                        {/* Rating */}
                        <div className="d-flex align-items-center gap-2 mb-3">
                            <div>{starGenerate(product.averageRating || 5)}</div>
                            <small className="text-muted">
                                ({reviews.length} {reviews.length === 1 ? 'review' : 'reviews'})
                            </small>
                        </div>

                        {/* Price */}
                        <div className="d-flex align-items-baseline gap-3 mb-3">
                            <span className="display-6 fw-bold text-success font-playfair">₹ {product.price}</span>
                            <span className="text-muted small">Inclusive of all taxes</span>
                        </div>

                        {/* Stock Indicator */}
                        <div className="mb-4">
                            {inStock ? (
                                <span className="text-success fw-semibold small d-inline-flex align-items-center gap-1">
                                    <FaCheckCircle /> In Stock ({product.stock} units available)
                                </span>
                            ) : (
                                <span className="text-danger fw-semibold small d-inline-flex align-items-center gap-1">
                                    <FaTimesCircle /> Currently Out of Stock
                                </span>
                            )}
                        </div>

                        {/* Description */}
                        <p className="text-secondary mb-4 leading-relaxed">
                            {product.description}
                        </p>

                        {/* Quantity and Actions */}
                        {inStock && (
                            <div className="d-flex flex-wrap align-items-center gap-3 mb-4">
                                <div className="d-flex align-items-center border rounded-pill overflow-hidden bg-light px-2 py-1">
                                    <button
                                        type="button"
                                        className="btn btn-sm btn-link text-dark fw-bold text-decoration-none px-2"
                                        onClick={() => setQuantity(q => Math.max(1, q - 1))}
                                    >
                                        -
                                    </button>
                                    <span className="px-3 fw-bold">{quantity}</span>
                                    <button
                                        type="button"
                                        className="btn btn-sm btn-link text-dark fw-bold text-decoration-none px-2"
                                        onClick={() => setQuantity(q => Math.min(product.stock, q + 1))}
                                    >
                                        +
                                    </button>
                                </div>

                                <button
                                    onClick={handleAddToCart}
                                    className="btn btn-primary-ayur text-white rounded-pill px-4 py-2 fw-semibold d-flex align-items-center gap-2 shadow-sm"
                                >
                                    <FaShoppingBag /> Add To Cart
                                </button>

                                <button
                                    onClick={handleBuyNow}
                                    className="btn btn-warning text-dark rounded-pill px-4 py-2 fw-semibold d-flex align-items-center gap-2 shadow-sm"
                                >
                                    <FaBolt /> Buy Now
                                </button>
                            </div>
                        )}

                        {/* Trust Highlights */}
                        <div className="row g-2 pt-3 border-top small text-muted">
                            <div className="col-6 col-md-4 d-flex align-items-center gap-2">
                                <FaLeaf className="text-success" /> 100% Ayurvedic
                            </div>
                            <div className="col-6 col-md-4 d-flex align-items-center gap-2">
                                <FaTruck className="text-success" /> Express Shipping
                            </div>
                            <div className="col-6 col-md-4 d-flex align-items-center gap-2">
                                <FaShieldAlt className="text-success" /> Authenticity Assured
                            </div>
                        </div>
                    </div>
                </div>
            </div>

            {/* Customer Reviews Section */}
            <div className="card border-0 shadow-sm rounded-4 p-4 p-md-5 mb-5 bg-white">
                <h4 className="fw-bold font-playfair text-dark mb-4">Customer Reviews & Ratings</h4>

                <div className="row g-5">
                    {/* Left: Review List */}
                    <div className="col-12 col-md-7">
                        {reviews.length === 0 ? (
                            <div className="text-center py-4 bg-light rounded-4 p-4">
                                <p className="text-muted mb-0 small">No reviews yet for this herbal medicine. Be the first to review!</p>
                            </div>
                        ) : (
                            <div className="space-y-3">
                                {reviews.map((rev, rIdx) => (
                                    <div key={rIdx} className="border-bottom pb-3 mb-3">
                                        <div className="d-flex justify-content-between align-items-center mb-1">
                                            <strong className="text-dark">
                                                {rev.user?.username || rev.userName || "Verified Ayurvedic Customer"}
                                            </strong>
                                            <div>{starGenerate(rev.rating || 5)}</div>
                                        </div>
                                        <p className="text-secondary small mb-1">{rev.review}</p>
                                        <small className="text-muted" style={{ fontSize: '11px' }}>
                                            {rev.date ? new Date(rev.date).toLocaleDateString() : 'Verified Buyer'}
                                        </small>
                                    </div>
                                ))}
                            </div>
                        )}
                    </div>

                    {/* Right: Add Review Form */}
                    <div className="col-12 col-md-5">
                        <div className="bg-light p-4 rounded-4">
                            <h5 className="fw-bold text-dark font-playfair mb-3">Write a Review</h5>
                            <form onSubmit={handleAddReview}>
                                <div className="mb-3">
                                    <label className="form-label small fw-semibold text-secondary">Your Rating</label>
                                    <div className="d-flex gap-2">
                                        {[1, 2, 3, 4, 5].map((star) => (
                                            <span
                                                key={star}
                                                onClick={() => setNewRating(star)}
                                                style={{ cursor: 'pointer', fontSize: '24px', color: star <= newRating ? '#ffc107' : '#e0e0e0' }}
                                            >
                                                ★
                                            </span>
                                        ))}
                                    </div>
                                </div>

                                <div className="mb-3">
                                    <label className="form-label small fw-semibold text-secondary">Your Experience & Feedback</label>
                                    <textarea
                                        className="form-control rounded-3"
                                        rows="4"
                                        placeholder="How did this Ayurvedic medicine help your health? Share details..."
                                        value={newReviewText}
                                        onChange={(e) => setNewReviewText(e.target.value)}
                                        required
                                    ></textarea>
                                </div>

                                <button
                                    type="submit"
                                    className="btn btn-primary-ayur text-white rounded-pill px-4 fw-semibold w-100"
                                    disabled={submittingReview}
                                >
                                    {submittingReview ? "Submitting..." : "Submit Review"}
                                </button>
                            </form>
                        </div>
                    </div>
                </div>
            </div>

            {/* Related Products */}
            {relatedProducts.length > 0 && (
                <div>
                    <h4 className="fw-bold font-playfair text-dark mb-4">Related Ayurvedic Formulations</h4>
                    <div className="row g-4">
                        {relatedProducts.map((relProd, idx) => {
                            const relImg = relProd.imageURL
                                ? `${Api.imageBaseUrl}${relProd.imageURL}`
                                : 'https://i.ibb.co/ZNB6jDz/ayurvedic-bowl.png';
                            return (
                                <div className="col-6 col-md-3" key={idx}>
                                    <div
                                        className="card h-100 border-0 shadow-sm rounded-4 p-3 card-ayur cursor-pointer transition-all"
                                        onClick={() => {
                                            navigate('/show-product-details', { state: { product: relProd } });
                                            window.scrollTo(0, 0);
                                        }}
                                        style={{ cursor: 'pointer' }}
                                    >
                                        <img
                                            src={relImg}
                                            alt={relProd.title}
                                            className="img-fluid object-fit-contain mx-auto mb-2"
                                            style={{ height: '140px' }}
                                        />
                                        <h6 className="fw-bold text-dark font-playfair mb-1 text-truncate">{relProd.title}</h6>
                                        <span className="text-success fw-bold small">₹ {relProd.price}</span>
                                    </div>
                                </div>
                            );
                        })}
                    </div>
                </div>
            )}
        </div>
    );
}

export default ShowProductDetails;