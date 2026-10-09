import axios from "axios";
import { useEffect, useRef, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import Api from '../WebApi/Api';

function EditProfile() {
    const nameInput = useRef();
    const emailInput = useRef();
    const contactInput = useRef();
    const [doctor, setDoctor] = useState({});
    const location = useLocation();
    const navigate = useNavigate();

    useEffect(() => {
        setDoctor(location.state.doctor);
        !sessionStorage.getItem('doctor-details') && navigate('/');
    }, [location.state.doctor]);

    const updateProfile = async () => {
        try {
            const email=emailInput.current.value;
            const doctorName=nameInput.current.value;
            const contactNumber=contactInput.current.value;
            const doctorId=doctor._id;
            const response = await axios.post(Api.doctorProfileUpdate, {email,doctorName,contactNumber,doctorId})
            sessionStorage.setItem('doctor-details',JSON.stringify(response.data.doctor))
            navigate('/doctor-dashboard');
        } catch (error) {
            console.error("Error updating profile:", error);
        }
    };

    return (
        <div className="container-fluid d-flex justify-content-center align-items-center">
            <div className="border-2 border-success" style={{ width: '80%' }}>
                <label className="my-2">Doctor Name</label>
                <input type='text' ref={nameInput} defaultValue={doctor.doctorName} className="form-control" />
                <label className="my-2">Email</label>
                <input type='email' ref={emailInput} defaultValue={doctor.email} className="form-control" />
                <label className="my-2">Contact</label>
                <input type='number' ref={contactInput} defaultValue={doctor.contactNumber} className="form-control mb-3" />
                <button className="btn btn-success my-2" onClick={updateProfile}>Change Details</button>
                <button className="btn btn-danger my-2 ms-3" onClick={() => navigate(-1)}>Cancel</button>
            </div>
        </div>
    );
}

export default EditProfile;
