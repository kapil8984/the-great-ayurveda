import { Navigate, Route, Routes } from "react-router-dom";
import DoctorAccount from "./components/doctor/DoctorAccount";
import DoctorDashboard from "./components/doctor/DoctorDashboard";
import "react-toastify/dist/ReactToastify.css";
import { ToastContainer } from "react-toastify";
import DoctorProfile from "./components/doctor/DoctorProfile";
import EditProfile from "./components/doctor/EditProfile";
import ChangePassword from "./components/doctor/ChangePassword";
import AddDetailsDoctor from "./components/doctor/AddDetailsDoctor";
import AdminDashboard from "./components/admin/AdminDashboard";
import DoctorRequestList from "./components/admin/DoctorRequestList";
import AdminAccount from "./components/admin/AdminAccount";
import ShowDoctorDetails from "./components/admin/ShowDoctorDetails";
import UserAccount from "./components/user/UserAccount";
import UserProfile from "./components/user/UserProfile";
import ActiveDoctor from "./components/user/ActiveDoctor";
import Home from "./components/Home";
import AddProduct from "./components/admin/AddProducts";
import HomeData from "./components/HomeData";
import ShowProductToUser from "./components/product/ShowProductToUser";
import ShowCart from "./components/user/ShowCart";
import ShowOrderDetails from "./components/user/ShowOrderDetails";
import ShowProductDetails from "./components/product/ShowProductDetails";
import AddYogaAdmin from "./components/admin/AddYoga";
import ShowYoga from "./components/yoga/ShowYoga";
import ShowYogaDetails from "./components/yoga/ShowYogaDetails";
import DiseasesSearch from "./components/diseases/DiseasesSearch";
import About from "./components/About";
import Contact from "./components/Contact";

export default function App() {
  return (
    <>
      <ToastContainer position="top-right" autoClose={3000} hideProgressBar={false} />
      <Routes>
        <Route path="/" element={<Home />}>
          <Route index={true} element={<HomeData />} />
          <Route path="get-all-products" element={<ShowProductToUser />} />
          <Route path="ShowYogaDetails" element={<ShowYogaDetails />} />
          <Route path="show-cart" element={<ShowCart />} />
          <Route path="order-details" element={<ShowOrderDetails />} />
          <Route path="show-product-details" element={<ShowProductDetails />} />
          <Route path="yoga" element={<ShowYoga />} />
          <Route path="diseases" element={<DiseasesSearch />} />
          <Route path="homeremedies" element={<DiseasesSearch />} />
          <Route path="doctorconsult" element={<ActiveDoctor />} />
          <Route path="profile" element={<UserProfile />} />
          <Route path="user-profile" element={<UserProfile />} />
          <Route path="about" element={<About />} />
          <Route path="contact" element={<Contact />} />
        </Route>

        <Route path="/user-account" element={<UserAccount />} />
        <Route path="/doctor-signIn" element={<DoctorAccount />} />

        <Route path="doctor-dashboard" element={<DoctorDashboard />}>
          <Route path="doctor-profile" element={<DoctorProfile />}>
            <Route path="edit-profile" element={<EditProfile />} />
            <Route path="change-password" element={<ChangePassword />} />
            <Route path="add-details-doctor" element={<AddDetailsDoctor />} />
          </Route>
        </Route>

        <Route path="admin-signIn" element={<AdminAccount />} />

        <Route path="admin-dashboard" element={<AdminDashboard />}>
          <Route path="add-yoga" element={<AddYogaAdmin />} />
          <Route path="pending-doctor-request" element={<DoctorRequestList />} />
          <Route path="show-doctor-details" element={<ShowDoctorDetails />} />
          <Route path="add-product" element={<AddProduct />} />
        </Route>

        {/* Fallback route */}
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </>
  );
}
