import { FaStar ,FaStarHalf} from "react-icons/fa";
import { AiOutlineStar } from "react-icons/ai";
const starGenerate=(rating)=>{
    const ratingStar=Array.from({length:5},(_,index)=>{
        let number=index+0.5;
        return (<span key={index}>
            {
                rating>=index+1 ? <FaStar color="orange" className="icon"/> : rating >= number ? <FaStarHalf color="orange" className="icon"/> : <AiOutlineStar color="orange" className="icon"/>
            }
        </span>);
    });
    return (
            <div className="icon-style">
                {ratingStar}
            </div>
    )
}
export default starGenerate;