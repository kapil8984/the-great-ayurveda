import Header from './Header';
import '../style/admin.css';
import { useEffect, useState } from "react";
import Sidebar from './Sidebar';
import Home from './Home';
import { useDispatch } from 'react-redux';
import { getPendingDoctor } from '../../redux-config/PendinDoctorSlice';
function AdminDashboard() {
    const dispatch=useDispatch();
    useEffect(()=>{
        dispatch(getPendingDoctor());
    },[])
    const [openSidebarToggle, setOpenSidebarToggle] = useState(false)
    const OpenSidebar = () => {
        setOpenSidebarToggle(!openSidebarToggle)
    }
    return <>
        <div className='grid-container'>
            <Header OpenSidebar={OpenSidebar} />
            <Sidebar openSidebarToggle={openSidebarToggle} OpenSidebar={OpenSidebar} />
            <Home/>
        </div>
    </>
}
export default AdminDashboard;