import { useState } from 'react'
import '../style/doctor.css'
import Header from './Header'
import Sidebar from './Sidebar'
import Home from './Home'
// import { useNavigate } from 'react-router-dom'
function DoctorDashboard() {
    const [openSidebarToggle, setOpenSidebarToggle] = useState(false)
    const OpenSidebar = () => {
        setOpenSidebarToggle(!openSidebarToggle)
    }
    
    return <>
        <div className='grid-container'>
            <Header OpenSidebar={OpenSidebar} />
            <Sidebar openSidebarToggle={openSidebarToggle} OpenSidebar={OpenSidebar} />
            <Home />
        </div>
    </>
}
export default DoctorDashboard;