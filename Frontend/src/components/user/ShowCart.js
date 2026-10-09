import React, { useEffect, useState } from "react";
import axios from "axios";
import Api from "../WebApi/Api";
import { useNavigate } from "react-router-dom";
import { toast } from "react-toastify";
import { FaTrash, FaShoppingBag, FaArrowLeft, FaCheckCircle, FaShieldAlt } from "react-icons/fa";

function ShowCart() {
    const navigate = useNavigate();
    const [cartItems, setCartItems] = useState([]);
    const [loading, setLoading] = useState(true);
    const [submittingOrder, setSubmittingOrder] = useState(false);

    // Delivery details
    const [shippingAddress, setShippingAddress] = useState("");
    const [deliveryNotes, setDeliveryNotes] = useState("");

    const currentUser = JSON.parse(sessionStorage.getItem('user-details') || 'null');

    useEffect(() => {
        if (!currentUser) {
            toast.info("Please sign in to view your cart");
            navigate('/user-account');
            return;
        }
        loadCart();
    }, []);

    const loadCart = async () => {
        try {
            setLoading(true);
            const userId = currentUser._id || currentUser.id;
            const response = await axios.post(Api.getCart, { userId });
            const items = response.data?.cart?.cartItems || [];
            setCartItems(items);
        } catch (error) {
            console.error("Cart fetch error:", error);
        } finally {
            setLoading(false);
        }
    };

    const handleQuantity = async (productId, delta) => {
        try {
            const userId = currentUser._id || currentUser.id;
            await axios.put(Api.updateQuantiy, { quantity: delta, productId, userId });
            
            // Optimistically update
            setCartItems(prev => prev.map(item => {
                const itemProdId = item.productId?._id || item.productId?.id;
                if (itemProdId === productId) {
                    const newQty = item.quantity + delta;
                    return { ...item, quantity: newQty };
                }
                return item;
            }).filter(item => item.quantity > 0));
        } catch (error) {
            console.error("Quantity update error:", error);
            toast.error("Could not update item quantity");
        }
    };

    const handleRemoveItem = async (productId) => {
        try {
            const userId = currentUser._id || currentUser.id;
            await axios.post(Api.removeFromCart, { userId, productId });
            setCartItems(prev => prev.filter(item => (item.productId?._id || item.productId?.id) !== productId));
            toast.info("Item removed from cart");
        } catch (error) {
            console.error("Remove item error:", error);
            toast.error("Could not remove item");
        }
    };

    const calculateSubtotal = () => {
        return cartItems.reduce((acc, item) => {
            const price = Number(item.productId?.price || 0);
            const qty = Number(item.quantity || 1);
            return acc + (price * qty);
        }, 0);
    };

    const handleCheckout = async (e) => {
        e.preventDefault();
        if (cartItems.length === 0) {
            toast.warning("Your cart is empty");
            return;
        }

        if (!shippingAddress.trim()) {
            toast.warning("Please enter your complete shipping delivery address");
            return;
        }

        try {
            setSubmittingOrder(true);
            const userId = currentUser._id || currentUser.id;
            
            const payload = {
                userId,
                shippingAddress: shippingAddress.trim(),
                deliveryNotes: deliveryNotes.trim(),
                products: cartItems.map(item => ({
                    productId: item.productId?._id || item.productId?.id,
                    quantity: item.quantity
                }))
            };

            const response = await axios.post(Api.addOrder, payload);
            toast.success(response.data.message || "Order placed successfully!");
            setCartItems([]);
            navigate('/order-details');
        } catch (error) {
            console.error("Checkout error:", error);
            const msg = error.response?.data?.message || "Could not complete order. Please check stock.";
            toast.error(msg);
        } finally {
            setSubmittingOrder(false);
        }
    };

    const subtotal = calculateSubtotal();

    if (loading) {
        return (
            <div className="container py-5 text-center">
                <div className="spinner-border text-success" role="status">
                    <span className="visually-hidden">Loading cart...</span>
                </div>
            </div>
        );
    }

    return (
        <div className="container py-5">
            {/* Header */}
            <div className="d-flex justify-content-between align-items-center mb-4">
                <div>
                    <h2 className="fw-bold font-playfair text-dark mb-1">Your Healing Cart</h2>
                    <p className="text-muted small mb-0">Review your selected Ayurvedic medicines before placing your order</p>
                </div>
                <button
                    onClick={() => navigate('/order-details')}
                    className="btn btn-outline-success rounded-pill px-4 btn-sm"
                >
                    View Order History
                </button>
            </div>

            {cartItems.length === 0 ? (
                <div className="text-center py-5 card border-0 shadow-sm rounded-4 p-5 mx-auto" style={{ maxWidth: '500px' }}>
                    <FaShoppingBag size={54} className="text-muted mx-auto mb-3" />
                    <h4 className="fw-semibold text-secondary font-playfair">Your cart is currently empty</h4>
                    <p className="text-muted small mb-4">Discover our hand-crafted herbal remedies, tonics, and natural oils.</p>
                    <button
                        className="btn btn-primary-ayur text-white rounded-pill px-4 mx-auto fw-semibold"
                        onClick={() => navigate('/get-all-products')}
                    >
                        Browse Herbal Pharmacy
                    </button>
                </div>
            ) : (
                <div className="row g-4">
                    {/* Left: Cart Items List */}
                    <div className="col-12 col-lg-8">
                        <div className="card border-0 shadow-sm rounded-4 p-4 bg-white mb-4">
                            <div className="table-responsive">
                                <table className="table align-middle">
                                    <thead className="table-light small text-secondary">
                                        <tr>
                                            <th>Product</th>
                                            <th>Price</th>
                                            <th className="text-center">Quantity</th>
                                            <th>Total</th>
                                            <th className="text-end">Action</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {cartItems.map((item, index) => {
                                            const prod = item.productId || {};
                                            const prodId = prod._id || prod.id;
                                            const imgSrc = prod.imageURL
                                                ? `${Api.imageBaseUrl}${prod.imageURL}`
                                                : 'https://i.ibb.co/ZNB6jDz/ayurvedic-bowl.png';
                                            const itemTotal = (prod.price * item.quantity).toFixed(2);

                                            return (
                                                <tr key={index}>
                                                    <td>
                                                        <div className="d-flex align-items-center gap-3">
                                                            <img
                                                                src={imgSrc}
                                                                alt={prod.title}
                                                                className="rounded-3 border object-fit-contain"
                                                                style={{ width: '64px', height: '64px' }}
                                                                onError={(e) => { e.target.onerror = null; e.target.src = 'https://i.ibb.co/ZNB6jDz/ayurvedic-bowl.png'; }}
                                                            />
                                                            <div>
                                                                <h6 className="fw-bold mb-0 text-dark small">{prod.title}</h6>
                                                                <small className="text-muted">{prod.brand || "Ayurveda"}</small>
                                                            </div>
                                                        </div>
                                                    </td>
                                                    <td className="fw-semibold text-dark">
                                                        ₹ {prod.price}
                                                    </td>
                                                    <td className="text-center">
                                                        <div className="d-inline-flex align-items-center border rounded-pill bg-light px-2 py-1">
                                                            <button
                                                                type="button"
                                                                className="btn btn-sm btn-link text-dark text-decoration-none fw-bold px-2 py-0"
                                                                onClick={() => handleQuantity(prodId, -1)}
                                                            >
                                                                -
                                                            </button>
                                                            <span className="px-2 small fw-bold">{item.quantity}</span>
                                                            <button
                                                                type="button"
                                                                className="btn btn-sm btn-link text-dark text-decoration-none fw-bold px-2 py-0"
                                                                onClick={() => handleQuantity(prodId, 1)}
                                                            >
                                                                +
                                                            </button>
                                                        </div>
                                                    </td>
                                                    <td className="fw-bold text-success">
                                                        ₹ {itemTotal}
                                                    </td>
                                                    <td className="text-end">
                                                        <button
                                                            onClick={() => handleRemoveItem(prodId)}
                                                            className="btn btn-link text-danger p-0"
                                                            title="Remove item"
                                                        >
                                                            <FaTrash />
                                                        </button>
                                                    </td>
                                                </tr>
                                            );
                                        })}
                                    </tbody>
                                </table>
                            </div>

                            <div className="d-flex justify-content-between align-items-center mt-3 pt-3 border-top">
                                <button
                                    onClick={() => navigate('/get-all-products')}
                                    className="btn btn-link text-success text-decoration-none small d-flex align-items-center gap-2 fw-semibold"
                                >
                                    <FaArrowLeft /> Continue Shopping
                                </button>
                                <span className="small text-muted">
                                    Subtotal: <strong>₹ {subtotal.toFixed(2)}</strong>
                                </span>
                            </div>
                        </div>
                    </div>

                    {/* Right: Checkout & Address Summary */}
                    <div className="col-12 col-lg-4">
                        <div className="card border-0 shadow-sm rounded-4 p-4 bg-white">
                            <h5 className="fw-bold font-playfair text-dark mb-3">Order Summary</h5>

                            <div className="space-y-2 mb-3 pb-3 border-bottom small text-secondary">
                                <div className="d-flex justify-content-between mb-2">
                                    <span>Items Subtotal ({cartItems.length}):</span>
                                    <span className="fw-semibold text-dark">₹ {subtotal.toFixed(2)}</span>
                                </div>
                                <div className="d-flex justify-content-between mb-2">
                                    <span>Ayurvedic Packaging:</span>
                                    <span className="text-success fw-semibold">FREE</span>
                                </div>
                                <div className="d-flex justify-content-between mb-2">
                                    <span>Standard Delivery:</span>
                                    <span className="text-success fw-semibold">FREE</span>
                                </div>
                            </div>

                            <div className="d-flex justify-content-between align-items-center mb-4">
                                <span className="fs-5 fw-bold text-dark font-playfair">Total Amount:</span>
                                <span className="fs-4 fw-bold text-success font-playfair">₹ {subtotal.toFixed(2)}</span>
                            </div>

                            {/* Shipping Delivery Form */}
                            <form onSubmit={handleCheckout}>
                                <div className="mb-3">
                                    <label className="form-label small fw-semibold text-secondary">Delivery Shipping Address</label>
                                    <textarea
                                        className="form-control rounded-3"
                                        rows="3"
                                        placeholder="Flat/House No., Street, City, State, Pincode"
                                        value={shippingAddress}
                                        onChange={(e) => setShippingAddress(e.target.value)}
                                        required
                                    ></textarea>
                                </div>

                                <div className="mb-4">
                                    <label className="form-label small fw-semibold text-secondary">Delivery Instructions (Optional)</label>
                                    <input
                                        type="text"
                                        className="form-control rounded-3"
                                        placeholder="e.g. Leave with security / Ring bell"
                                        value={deliveryNotes}
                                        onChange={(e) => setDeliveryNotes(e.target.value)}
                                    />
                                </div>

                                <button
                                    type="submit"
                                    className="btn btn-primary-ayur btn-lg text-white rounded-pill w-100 fw-semibold mb-3 shadow-sm d-flex align-items-center justify-content-center gap-2"
                                    disabled={submittingOrder}
                                >
                                    {submittingOrder ? (
                                        <span>
                                            <span className="spinner-border spinner-border-sm me-2" role="status"></span>
                                            Placing Order...
                                        </span>
                                    ) : (
                                        <>
                                            <FaCheckCircle /> Confirm & Place Order
                                        </>
                                    )}
                                </button>

                                <div className="text-center small text-muted d-flex align-items-center justify-content-center gap-1">
                                    <FaShieldAlt className="text-success" /> Safe & Certified Herbal Dispatch
                                </div>
                            </form>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}

export default ShowCart;
