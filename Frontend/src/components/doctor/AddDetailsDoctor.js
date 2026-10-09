import axios from "axios";
import { useEffect, useRef, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import Api from "../WebApi/Api";

function AddDetailsDoctor() {
    const navigate = useNavigate();
    const qualificationImageRef = useRef(null);
    const experienceRef = useRef(null);
    const genderRef = useRef(null);
    const languageRef = useRef(null);
    const clinicAddressRef = useRef(null);
    const specializationRef = useRef(null);
    const [doctor, setDoctor] = useState({});
    const [categorys, setCategorys] = useState([]);
    const [selectedSpecialization, setSelectedSpecialization] = useState("");
    const [morningTimes, setMorningTimes] = useState([]);
    const [afternoonTimes, setAfternoonTimes] = useState([]);
    const [eveningTimes, setEveningTimes] = useState([]);
    const location = useLocation();

    useEffect(() => {
        setDoctor(location.state.doctor);
        getCategory();
        !sessionStorage.getItem('doctor-details') && navigate('/');
    }, [location.state.doctor]);

    const getCategory = async () => {
        try {
            let response = await axios.get(Api.getCategory);
            setCategorys(response.data.categories);
        } catch (error) {
            console.log(error);
        }
    };

    const handleChangeDetails = async () => {
        try {
            // Construct formData
            const formData = new FormData();
            formData.append('qualificationImage', qualificationImageRef.current.files[0]);
            formData.append('experience', experienceRef.current.value);
            formData.append('gender', genderRef.current.value);
            formData.append('language', languageRef.current.value);
            formData.append('clinicAddress', clinicAddressRef.current.value);
            formData.append('specialization', selectedSpecialization);
            formData.append('doctorId', doctor._id);

            // Append only checked times to formData
            morningTimes
                .filter(time => morningTimes.includes(time))
                .forEach(time => formData.append('morningTimes', time));

            afternoonTimes
                .filter(time => afternoonTimes.includes(time))
                .forEach(time => formData.append('afternoonTimes', time));

            eveningTimes
                .filter(time => eveningTimes.includes(time))
                .forEach(time => formData.append('eveningTimes', time));

            // Post data
            const response = await axios.post(Api.addDoctorDetails, formData, {
                headers: {
                    'Content-Type': 'multipart/form-data'
                }
            });
            console.log(response.data);
            navigate('/doctor-dashboard');
        } catch (error) {
            console.error(error);
        }
    };

    const generateTimeRange = (start, end) => {
        const times = [];
        for (let i = start; i <= end; i++) {
            let hour = i < 12 ? i : i - 12;
            let period = i < 12 ? 'AM' : 'PM';
            times.push(`${hour === 0 ? 12 : hour}:${ '00' } ${period}`);
        }
        let checkboxes=document.getElementsByClassName('time');
        for (let index = 0; index < checkboxes.length; index++) {
            if(checkboxes[index].checked)
            {
                checkboxes[index].nextSibling.style.backgroundColor='lightgreen';
                checkboxes[index].nextSibling.style.color='white';
            }
            else{
                checkboxes[index].nextSibling.style.backgroundColor='white';
                checkboxes[index].nextSibling.style.color='grey';
                checkboxes[index].nextSibling.style.border='1px solid lightgreen';
                
            }
        }
        return times;
    };

    const morningTimeRange = generateTimeRange(9, 11);
    const afternoonTimeRange = generateTimeRange(13, 16);
    const eveningTimeRange = generateTimeRange(16, 18);

    return (
        <div className="container-fluid d-flex justify-content-center align-items-center">
            <div className="border-2 border-success" style={{ width: '80%' }}>
                <label className="my-2">Qualification Image</label>
                <input type='file' ref={qualificationImageRef} className="form-control" />
                <label className="my-2">Experience in Year</label>
                <input type='number' ref={experienceRef} className="form-control" />
                <label className="my-2">Gender</label>
                <input type='text' ref={genderRef} className="form-control mb-3" />
                <label className="my-2">Language</label>
                <input type='text' ref={languageRef} className="form-control mb-3" />
                <label className="my-2">Clinic Address</label>
                <input type='text' ref={clinicAddressRef} className="form-control mb-3" />
                <label className="my-2">Specialization</label>
                <select
                    className="form-control"
                    ref={specializationRef}
                    value={selectedSpecialization}
                    onChange={(e) => setSelectedSpecialization(e.target.value)}>
                    {categorys.map((category, index) => (
                        <option key={index} value={category._id}>
                            {category.name}
                        </option>
                    ))}
                </select>
                <label className="mt-3">Morning</label>
                <div className="my-3 d-flex">
                    {morningTimeRange.map((time, index) => (
                        <div key={index} className="d-flex">
                            <input className="time d-none"
                                id={'mor'+index}
                                type="checkbox"
                                checked={morningTimes.includes(time)}
                                onChange={() => setMorningTimes(prevTimes => prevTimes.includes(time) ? prevTimes.filter(t => t !== time) : [...prevTimes, time])}
                            />
                             <label for={'mor'+index} style={{border:'1px solid blue',fontWeight:'600',color:'grey',cursor:'pointer'}} className="p-1 mx-1 rounded">{time}</label>
                        </div>
                    ))}
                </div>
                <label>Afternoon</label>
                <div className="my-3 d-flex">
                    {afternoonTimeRange.map((time, index) => (
                        <div key={index} className="d-flex">
                            <input id={'aft'+index} className="time p-2 d-none"
                                type="checkbox"
                                checked={afternoonTimes.includes(time)}
                                onChange={() => setAfternoonTimes(prevTimes => prevTimes.includes(time) ? prevTimes.filter(t => t !== time) : [...prevTimes, time])}
                            />
                             <label for={'aft'+index} style={{border:'1px solid blue',fontWeight:'600',color:'grey',cursor:'pointer'}} className="p-1 mx-1 rounded">{time}</label>
                        </div>
                    ))}
                </div>
                <label>Evening</label>
                <div className="my-3 d-flex">

                    {eveningTimeRange.map((time, index) => (
                        <div key={index} className="d-flex">
                            <input id={'eve'+index} className="time  d-none"
                                type="checkbox"
                                checked={eveningTimes.includes(time)}
                                onChange={() => setEveningTimes(prevTimes => prevTimes.includes(time) ? prevTimes.filter(t => t !== time) : [...prevTimes, time])}
                            />
                            <label for={'eve'+index} style={{border:'1px solid lightgreen',fontWeight:'600',color:'grey',cursor:'pointer'}} className="p-1 mx-1 rounded">{time}</label>
                        </div>
                    ))}
                </div>
                <button className="btn btn-success my-2" onClick={handleChangeDetails}>Change Details</button>
                <button className="btn btn-danger  my-2 ms-3" onClick={() => navigate(-1)}>Cancel</button>
            </div>
        </div>
    );
}

export default AddDetailsDoctor;
