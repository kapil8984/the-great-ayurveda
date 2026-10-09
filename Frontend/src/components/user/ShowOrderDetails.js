import React, { useEffect, useState } from "react";
import axios from "axios";
import Api from "../WebApi/Api";
import { toast } from "react-toastify";
import { useNavigate } from "react-router-dom";
import { FaBox, FaStar, FaCalendarAlt, FaMapMarkerAlt } from "react-icons/fa";

function ShowOrderDetails() {
    const navigate = useNavigate();
    const [orders, setOrders] = useState([]);
    const [loading, setLoading] = useState(true);

    // Modal state for rating & review
    const [showModal, setShowModal] = useState(false);
    const [selectedProduct, setSelectedProduct] = useState(null);
    const [starRating, setStarRating] = useState(5);
    const [reviewComment, setReviewComment] = useState("");
    const [isUpdatingReview, setIsUpdatingReview] = useState(false);
    const [submittingReview, setSubmittingReview] = useState(false);

    const currentUser = JSON.parse(sessionStorage.getItem('user-details') || 'null');

    useEffect(() => {
        if (!currentUser) {
            toast.info("Please sign in to view your order history");
            navigate('/user-account');
            return;
        }
        loadOrders();
    }, []);

    const loadOrders = async () => {
        try {
            setLoading(true);
            const userId = currentUser._id || currentUser.id;
            const response = await axios.post(Api.getOrderDetails, { userId });
            const list = response.data.orders || [];
            setOrders(list);
        } catch (error) {
            console.error("Order fetch error:", error);
            toast.error("Failed to load your orders");
        } finally {
            setLoading(false);
        }
    };

    const handleOpenReviewModal = (productItem) => {
        const prod = productItem.product || {};
        setSelectedProduct(prod);
        if (productItem.review) {
            setIsUpdatingReview(true);
            setStarRating(productItem.review.rating || 5);
            setReviewComment(productItem.review.review || "");
        } else {
            setIsUpdatingReview(false);
            setStarRating(5);
            setReviewComment("");
        }
        setShowModal(true);
    };

    const handleSubmitReview = async (e) => {
        e.preventDefault();
        if (!selectedProduct) return;

        try {
            setSubmittingReview(true);
            const userId = currentUser._id || currentUser.id;
            const prodId = selectedProduct._id || selectedProduct.id;

            const payload = {
                userId,
                productId: prodId,
                rating: starRating,
                review: reviewComment.trim()
            };

            if (isUpdatingReview) {
                await axios.put(Api.updateReviewAndRating, payload);
                toast.success("Review updated successfully!");
            } else {
                await axios.post(Api.addRateAndReviewUser, payload);
                toast.success("Thank you! Review saved.");
            }

            setShowModal(false);
            loadOrders();
        } catch (error) {
            console.error("Review error:", error);
            toast.error("Could not save review");
        } finally {
            setSubmittingReview(false);
        }
    };

    if (loading) {
        return (
            <div className="container py-5 text-center">
                <div className="spinner-border text-success" role="status">
                    <span className="visually-hidden">Loading orders...</span>
                </div>
            </div>
        );
    }

    return (
        <div className="container py-5">
            {/* Header */}
            <div className="d-flex justify-content-between align-items-center mb-4">
                <div>
                    <h2 className="fw-bold font-playfair text-dark mb-1">Your Order History</h2>
                    <p className="text-muted small mb-0">Track all your previous Ayurvedic medicine shipments and write reviews</p>
                </div>
                <button
                    onClick={() => navigate('/get-all-products')}
                    className="btn btn-outline-success rounded-pill px-4 btn-sm"
                >
                    Explore Medicines
                </button>
            </div>

            {orders.length === 0 ? (
                <div className="text-center py-5 card border-0 shadow-sm rounded-4 p-5 mx-auto" style={{ maxWidth: '500px' }}>
                    <FaBox size={50} className="text-muted mx-auto mb-3" />
                    <h4 className="fw-semibold text-secondary font-playfair">No Orders Found</h4>
                    <p className="text-muted small mb-4">You have not placed any orders yet. Visit our Ayurvedic pharmacy to order herbal medicines.</p>
                    <button
                        className="btn btn-primary-ayur text-white rounded-pill px-4 mx-auto fw-semibold"
                        onClick={() => navigate('/get-all-products')}
                    >
                        Start Shopping
                    </button>
                </div>
            ) : (
                <div className="space-y-4">
                    {orders.map((order, oIdx) => {
                        const orderDate = new Date(order.orderDate).toLocaleDateString('en-US', {
                            year: 'numeric',
                            month: 'long',
                            day: 'numeric'
                        });

                        const orderTotal = order.totalAmount || (
                            order.products?.reduce((acc, p) => acc + (p.quantity * (p.unitPrice || p.product?.price || 0)), 0)
                        ) || 0;

                        return (
                            <div className="card border-0 shadow-sm rounded-4 mb-4 overflow-hidden bg-white" key={oIdx}>
                                {/* Order Header */}
                                <div className="card-header bg-light py-3 px-4 border-0 d-flex flex-wrap justify-content-between align-items-center gap-2">
                                    <div className="d-flex align-items-center gap-3">
                                        <span className="badge bg-success-subtle text-success px-3 py-2 rounded-pill fw-semibold">
                                            Order #{order.id || order._id || (oIdx + 1)}
                                        </span>
                                        <span className="small text-muted d-flex align-items-center gap-1">
                                            <FaCalendarAlt /> Placed on: {orderDate}
                                        </span>
                                    </div>

                                    <div className="d-flex align-items-center gap-3">
                                        <span className={`badge rounded-pill px-3 py-1 ${
                                            order.status === 'Delivered' ? 'bg-success-subtle text-success' :
                                            order.status === 'Shipped' ? 'bg-info-subtle text-info' :
                                            'bg-warning-subtle text-dark'
                                        }`}>
                                            {order.status || 'Confirmed'}
                                        </span>
                                        <span className="fw-bold text-success fs-5">
                                            Total: ₹ {Number(orderTotal).toFixed(2)}
                                        </span>
                                    </div>
                                </div>

                                {/* Order Items Body */}
                                <div className="card-body p-4">
                                    {order.shippingAddress && (
                                        <div className="mb-3 p-2 bg-light rounded-3 small text-secondary d-flex align-items-center gap-2">
                                            <FaMapMarkerAlt className="text-success" />
                                            <span><strong>Shipping Address:</strong> {order.shippingAddress}</span>
                                        </div>
                                    )}

                                    <div className="space-y-3">
                                        {order.products?.map((item, pIdx) => {
                                            const prod = item.product || {};
                                            const price = item.unitPrice || prod.price || 0;
                                            const itemSubtotal = (price * item.quantity).toFixed(2);
                                            const imgSrc = prod.imageURL
                                                ? `${Api.imageBaseUrl}${prod.imageURL}`
                                                : 'https://i.ibb.co/ZNB6jDz/ayurvedic-bowl.png';

                                            return (
                                                <div className="d-flex flex-wrap align-items-center justify-content-between border-bottom pb-3 mb-3 gap-3" key={pIdx}>
                                                    <div className="d-flex align-items-center gap-3">
                                                        <img
                                                            src={imgSrc}
                                                            alt={prod.title}
                                                            className="rounded-3 border object-fit-contain"
                                                            style={{ width: '70px', height: '70px' }}
                                                            onError={(e) => { e.target.onerror = null; e.target.src = 'https://i.ibb.co/ZNB6jDz/ayurvedic-bowl.png'; }}
                                                        />
                                                        <div>
                                                            <h6 className="fw-bold mb-1 text-dark">{prod.title}</h6>
                                                            <small className="text-muted d-block">
                                                                Unit Price: ₹ {price} • Quantity: {item.quantity}
                                                            </small>
                                                        </div>
                                                    </div>

                                                    <div className="d-flex align-items-center gap-4">
                                                        <span className="fw-bold text-dark fs-6">
                                                            ₹ {itemSubtotal}
                                                        </span>

                                                        <button
                                                            onClick={() => handleOpenReviewModal(item)}
                                                            className={`btn btn-sm rounded-pill px-3 d-flex align-items-center gap-1 ${
                                                                item.review ? 'btn-outline-secondary' : 'btn-outline-success'
                                                            }`}
                                                        >
                                                            <FaStar className="text-warning" />
                                                            {item.review ? 'Edit Review' : 'Rate Product'}
                                                        </button>
                                                    </div>
                                                </div>
                                            );
                                        })}
                                    </div>
                                </div>
                            </div>
                        );
                    })}
                </div>
            )}

            {/* Clean React Modal for Rate & Review */}
            {showModal && selectedProduct && (
                <div className="modal show d-block" tabIndex="-1" style={{ backgroundColor: 'rgba(0,0,0,0.5)', backdropFilter: 'blur(3px)' }}>
                    <div className="modal-dialog modal-dialog-centered">
                        <div className="modal-content rounded-4 border-0 shadow-lg">
                            <div className="modal-header bg-success text-white px-4 py-3 border-0">
                                <h5 className="modal-title font-playfair fw-bold">
                                    {isUpdatingReview ? 'Update Your Review' : 'Rate & Review Medicine'}
                                </h5>
                                <button
                                    type="button"
                                    className="btn-close btn-close-white"
                                    onClick={() => setShowModal(false)}
                                    aria-label="Close"
                                ></button>
                            </div>

                            <form onSubmit={handleSubmitReview}>
                                <div className="modal-body p-4">
                                    <h6 className="fw-bold text-dark mb-2">{selectedProduct.title}</h6>
                                    
                                    <div className="mb-3">
                                        <label className="form-label small fw-semibold text-secondary">Your Rating</label>
                                        <div className="d-flex gap-2">
                                            {[1, 2, 3, 4, 5].map((star) => (
                                                <span
                                                    key={star}
                                                    onClick={() => setStarRating(star)}
                                                    style={{ cursor: 'pointer', fontSize: '28px', color: star <= starRating ? '#ffc107' : '#e0e0e0' }}
                                                >
                                                    ★
                                                </span>
                                            ))}
                                        </div>
                                    </div>

                                    <div className="mb-3">
                                        <label className="form-label small fw-semibold text-secondary">Your Review & Comments</label>
                                        <textarea
                                            className="form-control rounded-3"
                                            rows="4"
                                            value={reviewComment}
                                            onChange={(e) => setReviewComment(e.target.value)}
                                            placeholder="Describe how effective this Ayurvedic remedy was for your condition..."
                                            required
                                        ></textarea>
                                    </div>
                                </div>

                                <div className="modal-footer border-top px-4 py-3">
                                    <button
                                        type="button"
                                        className="btn btn-outline-secondary rounded-pill px-4"
                                        onClick={() => setShowModal(false)}
                                    >
                                        Close
                                    </button>
                                    <button
                                        type="submit"
                                        className="btn btn-primary-ayur text-white rounded-pill px-4 fw-semibold"
                                        disabled={submittingReview}
                                    >
                                        {submittingReview ? "Saving..." : (isUpdatingReview ? "Update Review" : "Save Review")}
                                    </button>
                                </div>
                            </form>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}

export default ShowOrderDetails;
