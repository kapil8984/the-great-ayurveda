import React, { useEffect, useRef, useState } from "react";
import Api from "../WebApi/Api";
import axios from "axios";
import { toast } from "react-toastify";
import { FaSpa } from "react-icons/fa";

function AddYogaAdmin() {
    const YogaName = useRef(null);
    const DescriptionRef = useRef(null);
    const BenefitsRef = useRef(null);
    const InstructionsRef = useRef(null);
    const imageUrlRef = useRef(null);
    const videoUrlRef = useRef(null);
    const CategoryRef = useRef(null);
    const [categories, setCategories] = useState([]);
    const [submitting, setSubmitting] = useState(false);

    const emptyAllRef = () => {
        if (YogaName.current) YogaName.current.value = '';
        if (DescriptionRef.current) DescriptionRef.current.value = '';
        if (BenefitsRef.current) BenefitsRef.current.value = '';
        if (InstructionsRef.current) InstructionsRef.current.value = '';
        if (imageUrlRef.current) imageUrlRef.current.value = '';
        if (videoUrlRef.current) videoUrlRef.current.value = '';
    };

    const handleSubmit = async (e) => {
        e.preventDefault();
        try {
            setSubmitting(true);
            const formData = {
                yogaName: YogaName.current.value.trim(),
                Description: DescriptionRef.current.value.trim(),
                benefits: BenefitsRef.current.value.split(',').map(b => b.trim()).filter(Boolean),
                instructions: InstructionsRef.current.value.trim(),
                imageUrl: imageUrlRef.current.value.trim(),
                videoUrl: videoUrlRef.current.value.trim(),
                categoryId: CategoryRef.current.value
            };

            const response = await axios.post(Api.AddYoga, formData);
            toast.success(response.data.message || "Yoga practice added successfully!");
            emptyAllRef();
        } catch (error) {
            console.error('Error adding yoga:', error);
            const msg = error.response?.data?.message || "Failed to add yoga";
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
                        <FaSpa size={24} />
                    </div>
                    <div>
                        <h3 className="fw-bold font-playfair text-dark mb-1">Add Therapeutic Yoga Asana</h3>
                        <p className="text-muted small mb-0">Publish guided yogic asanas, benefits, and video tutorials</p>
                    </div>
                </div>

                <form onSubmit={handleSubmit}>
                    <div className="row g-3">
                        <div className="col-12 col-md-6">
                            <label htmlFor="yogaName" className="form-label small fw-semibold text-secondary">Asana Name (Sanskrit / English)</label>
                            <input type="text" className="form-control rounded-3" id="yogaName" ref={YogaName} placeholder="e.g. Bhujangasana (Cobra Pose)" required />
                        </div>

                        <div className="col-12 col-md-6">
                            <label className="form-label small fw-semibold text-secondary">Associated Category</label>
                            <select ref={CategoryRef} className="form-select rounded-3" required>
                                {categories.map((cat, index) => (
                                    <option key={index} value={cat._id || cat.id}>
                                        {cat.name}
                                    </option>
                                ))}
                            </select>
                        </div>

                        <div className="col-12 col-md-6">
                            <label htmlFor="imageUrl" className="form-label small fw-semibold text-secondary">Posture Image URL</label>
                            <input type="url" className="form-control rounded-3" id="imageUrl" ref={imageUrlRef} placeholder="https://example.com/pose.jpg" required />
                        </div>

                        <div className="col-12 col-md-6">
                            <label htmlFor="videoUrl" className="form-label small fw-semibold text-secondary">YouTube Tutorial Video URL</label>
                            <input type="url" className="form-control rounded-3" id="videoUrl" ref={videoUrlRef} placeholder="https://www.youtube.com/watch?v=..." />
                        </div>

                        <div className="col-12">
                            <label htmlFor="benefits" className="form-label small fw-semibold text-secondary">Key Benefits (Comma separated)</label>
                            <input type="text" className="form-control rounded-3" id="benefits" ref={BenefitsRef} placeholder="e.g. Relieves spinal stiffness, Improves digestion, Calms stress" required />
                        </div>

                        <div className="col-12">
                            <label htmlFor="description" className="form-label small fw-semibold text-secondary">Overview & Therapeutic Purpose</label>
                            <textarea className="form-control rounded-3" id="description" rows="3" ref={DescriptionRef} placeholder="Explain how this asana balances Vata, Pitta, or Kapha..." required></textarea>
                        </div>

                        <div className="col-12">
                            <label htmlFor="instructions" className="form-label small fw-semibold text-secondary">Step-by-Step Breathing & Posture Instructions</label>
                            <textarea className="form-control rounded-3" id="instructions" rows="4" ref={InstructionsRef} placeholder="1. Lie flat on stomach. 2. Place palms near shoulders. 3. Inhale and slowly lift chest..." required></textarea>
                        </div>
                    </div>

                    <div className="mt-4 pt-3 border-top d-flex justify-content-end gap-2">
                        <button type="button" className="btn btn-outline-secondary rounded-pill px-4" onClick={emptyAllRef}>
                            Clear Form
                        </button>
                        <button type="submit" className="btn btn-primary-ayur text-white rounded-pill px-4 fw-semibold" disabled={submitting}>
                            {submitting ? "Saving..." : "Publish Yoga"}
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
}

export default AddYogaAdmin;
