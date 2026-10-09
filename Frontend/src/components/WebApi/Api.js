import axios from 'axios';

export const API_BASE_URL = process.env.REACT_APP_API_URL || 'http://localhost:5100';
export const IMAGE_BASE_URL = `${API_BASE_URL}/images/`;

// Central Axios instance with JWT Authorization interceptor
export const apiClient = axios.create({
  baseURL: API_BASE_URL,
});

apiClient.interceptors.request.use(
  (config) => {
    const token = sessionStorage.getItem('token') || 
                  sessionStorage.getItem('user-token') || 
                  sessionStorage.getItem('doctor-token') || 
                  sessionStorage.getItem('admin-token');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => Promise.reject(error)
);

// Endpoints mapping
const Api = {
  baseUrl: API_BASE_URL,
  imageBaseUrl: IMAGE_BASE_URL,

  // Doctor
  doctorSignUp: `${API_BASE_URL}/doctor/sign-up`,
  doctorSignIn: `${API_BASE_URL}/doctor/sign-in`,
  doctorProfileUpdate: `${API_BASE_URL}/doctor/update-profile`,
  changePassword: `${API_BASE_URL}/doctor/change-password`,
  addProfilePicture: `${API_BASE_URL}/doctor/add-profile-picture`,
  addDoctorDetails: `${API_BASE_URL}/doctor/add-doctor-details`,
  getDoctorDetails: `${API_BASE_URL}/doctor/get-doctor-details`,
  getActiveDoctors: `${API_BASE_URL}/doctor/active`,
  getAllDoctors: `${API_BASE_URL}/doctor/doctor-list`,

  // Admin
  adminSignIn: `${API_BASE_URL}/admin/sign-in`,
  adminSignUp: `${API_BASE_URL}/admin/sign-up`,
  getPendingDoctor: `${API_BASE_URL}/admin/get-pending-doctor`,
  approveDoctorRequestAccept: `${API_BASE_URL}/admin/approve-doctor-request-accept`,
  approveDoctorRequestReject: `${API_BASE_URL}/admin/approve-doctor-request-reject`,
  getAdminDashboard: `${API_BASE_URL}/api/admin/dashboard`,

  // User
  userSignUp: `${API_BASE_URL}/user/signup`,
  userSignIn: `${API_BASE_URL}/user/signin`,
  userProfile: `${API_BASE_URL}/user/profile`,
  userUpdateProfile: `${API_BASE_URL}/user/update-profile`,
  forgotPassword: `${API_BASE_URL}/user/forgot-password`,
  resetPassword: `${API_BASE_URL}/api/auth/reset-password`,

  // Products
  getAllProduct: `${API_BASE_URL}/product/getall`,
  getFilteredProducts: `${API_BASE_URL}/api/products`,
  getProductById: `${API_BASE_URL}/product/getbyid`,
  addOneProduct: `${API_BASE_URL}/product/addone`,
  deleteProduct: `${API_BASE_URL}/product/delete`,
  getProductsByCategory: `${API_BASE_URL}/product/by-category`,

  // Category
  getCategory: `${API_BASE_URL}/category/get-category`,
  addOneCategory: `${API_BASE_URL}/category/addone`,
  saveInBulkCategory: `${API_BASE_URL}/category/save-in-bulk`,
  deleteCategory: `${API_BASE_URL}/category/delete-category`,

  // Cart
  addToCart: `${API_BASE_URL}/cart/add-to-cart`,
  getCart: `${API_BASE_URL}/cart/getCart`,
  showCart: `${API_BASE_URL}/cart/getCart`,
  updateQuantiy: `${API_BASE_URL}/cart/product-quantity`,
  removeFromCart: `${API_BASE_URL}/cart/removeCart`,

  // Orders
  addOrder: `${API_BASE_URL}/order/add-order`,
  getOrderDetails: `${API_BASE_URL}/order/get-order-details`,
  getAllOrders: `${API_BASE_URL}/api/orders`,

  // Yoga
  getAllYoga: `${API_BASE_URL}/yoga/getAll`,
  deleteYoga: `${API_BASE_URL}/yoga/removeYogaByName`,
  deleteYogaById: `${API_BASE_URL}/yoga/removeYogaById`,
  AddYoga: `${API_BASE_URL}/yoga/addYoga`,
  BulkAddYoga: `${API_BASE_URL}/yoga/add-in-bulk-yoga`,
  UpdateYogaById: `${API_BASE_URL}/yoga/updateYogaById`,
  getReviewForYoga: `${API_BASE_URL}/yoga/get-review-for-yoga`,
  addRateAndReviewUserYoga: `${API_BASE_URL}/yoga/add-yoga-review`,
  getReviewForYogaByUser: `${API_BASE_URL}/yoga/getYogaReview`,
  updateYogaReviewAndRating: `${API_BASE_URL}/yoga/updateYogaReview`,

  // Product Reviews & Ratings
  addRateAndReviewUser: `${API_BASE_URL}/rate-review/add-user-review-and-rating`,
  getProductReviewAndRating: `${API_BASE_URL}/rate-review/get-product-review`,
  updateReviewAndRating: `${API_BASE_URL}/rate-review/change-review`,
  removeRating: `${API_BASE_URL}/rate-review/remove-rating`,

  // Appointments (Real Doctor Consultation System)
  bookAppointment: `${API_BASE_URL}/api/appointments`,
  getDoctorAppointments: `${API_BASE_URL}/api/appointments/doctor`,
  getUserAppointments: `${API_BASE_URL}/api/appointments/user`,
  updateAppointmentStatus: `${API_BASE_URL}/api/appointments`,
};

export default Api;