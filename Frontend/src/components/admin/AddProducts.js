import React, { useState, useRef, useEffect } from 'react';
import axios from 'axios';
import Api from '../WebApi/Api';
import { toast } from 'react-toastify';
import { FaBoxOpen } from 'react-icons/fa';

function AddProduct() {
    const titleRef = useRef(null);
    const brandRef = useRef(null);
    const priceRef = useRef(null);
    const descriptionRef = useRef(null);
    const stockRef = useRef(null);
    const fileInputRef = useRef(null);
    const CategoryRef = useRef(null);
    const [categories, setCategories] = useState([]);
    const [submitting, setSubmitting] = useState(false);

    const emptyAllRef = () => {
        if (titleRef.current) titleRef.current.value = '';
        if (brandRef.current) brandRef.current.value = '';
        if (priceRef.current) priceRef.current.value = '';
        if (descriptionRef.current) descriptionRef.current.value = '';
        if (stockRef.current) stockRef.current.value = '';
        if (fileInputRef.current) fileInputRef.current.value = '';
    };

    const handleSubmit = async (e) => {
        e.preventDefault();
        try {
            setSubmitting(true);
            const formData = new FormData();
            formData.append('title', titleRef.current.value);
            formData.append('brand', brandRef.current.value);
            formData.append('price', priceRef.current.value);
            formData.append('description', descriptionRef.current.value);
            formData.append('stock', stockRef.current.value);
            formData.append('category', CategoryRef.current.value);
            if (fileInputRef.current?.files?.[0]) {
                formData.append('image', fileInputRef.current.files[0]);
            }

            const response = await axios.post(Api.addOneProduct, formData, {
                headers: {
                    'Content-Type': 'multipart/form-data'
                }
            });
            toast.success(response.data.message || "Product added to catalog successfully!");
            emptyAllRef();
        } catch (error) {
            console.error('Error adding product:', error);
            const msg = error.response?.data?.message || "Failed to add product";
            toast.error(msg);
        } finally {
            setSubmitting(false);
        }
    };

    const getCategories = async () => {
        try {
            const response = await axios.get(Api.getCategory);
            const list = response.data.categories || response.data.category || [];
            setCategories(list);
        } catch (error) {
            console.error(error);
        }
    };

    useEffect(() => {
        getCategories();
    }, []);

    return (
        <div className="container py-4">
            <div className="card border-0 shadow-sm rounded-4 p-4 p-md-5 bg-white mx-auto" style={{ maxWidth: '800px' }}>
                <div className="d-flex align-items-center gap-3 mb-4 pb-3 border-bottom">
                    <div className="rounded-circle bg-success-subtle text-success p-3">
                        <FaBoxOpen size={24} />
                    </div>
                    <div>
                        <h3 className="fw-bold font-playfair text-dark mb-1">Add Herbal Product</h3>
                        <p className="text-muted small mb-0">Publish an authentic Ayurvedic remedy into the central store</p>
                    </div>
                </div>

                <form onSubmit={handleSubmit}>
                    <div className="row g-3">
                        <div className="col-12 col-md-6">
                            <label htmlFor="title" className="form-label small fw-semibold text-secondary">Product Title</label>
                            <input type="text" className="form-control rounded-3" id="title" ref={titleRef} placeholder="e.g. Ashwagandha Organic Capsules" required />
                        </div>

                        <div className="col-12 col-md-6">
                            <label htmlFor="brand" className="form-label small fw-semibold text-secondary">Brand / Manufacturer</label>
                            <input type="text" className="form-control rounded-3" id="brand" ref={brandRef} placeholder="e.g. Dabur, Baidyanath, or Pure Ayurveda" required />
                        </div>

                        <div className="col-12 col-md-6">
                            <label htmlFor="price" className="form-label small fw-semibold text-secondary">Selling Price (₹)</label>
                            <input type="number" className="form-control rounded-3" id="price" step="0.01" min="0" ref={priceRef} placeholder="e.g. 450.00" required />
                        </div>

                        <div className="col-12 col-md-6">
                            <label htmlFor="stock" className="form-label small fw-semibold text-secondary">Initial Inventory Stock</label>
                            <input type="number" className="form-control rounded-3" id="stock" min="0" ref={stockRef} placeholder="e.g. 100" required />
                        </div>

                        <div className="col-12 col-md-6">
                            <label className="form-label small fw-semibold text-secondary">Ayurveda Category</label>
                            <select ref={CategoryRef} className="form-select rounded-3" required>
                                {categories.map((cat, index) => (
                                    <option key={index} value={cat._id || cat.id}>
                                        {cat.name}
                                    </option>
                                ))}
                            </select>
                        </div>

                        <div className="col-12 col-md-6">
                            <label htmlFor="productImage" className="form-label small fw-semibold text-secondary">Product Image</label>
                            <input type="file" className="form-control rounded-3" id="productImage" ref={fileInputRef} accept="image/*" required />
                        </div>

                        <div className="col-12">
                            <label htmlFor="description" className="form-label small fw-semibold text-secondary">Detailed Description & Benefits</label>
                            <textarea className="form-control rounded-3" id="description" rows="4" ref={descriptionRef} placeholder="Describe the therapeutic properties, herbs used, and dosage..." required></textarea>
                        </div>
                    </div>

                    <div className="mt-4 pt-3 border-top d-flex justify-content-end gap-2">
                        <button type="button" className="btn btn-outline-secondary rounded-pill px-4" onClick={emptyAllRef}>
                            Clear Form
                        </button>
                        <button type="submit" className="btn btn-primary-ayur text-white rounded-pill px-4 fw-semibold" disabled={submitting}>
                            {submitting ? "Adding Product..." : "Save Product"}
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
}

export default AddProduct;
