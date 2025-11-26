/**
    * To scroll to the top of document.
    * @author   Abdul Kareem
    * @param    {object} item                       Required. The object.
    * @param    {string} key                        Required. The key to search in object.
    * @return   {undefined|string|number|boolean}   Value for key in object, or undefined if key not found.
    * 
    * Sample:
    * Declare the following code in page.
    * <ul id="scroll-topContainter" class="mfb-component--br d-none" data-mfb-toggle="hover">
        <li class="mfb-component__wrap">
            <a href="#" id="scroll-topButton" class="mfb-component__button--main text-white">
                <i class="mfb-component__main-icon--resting fas fa-chevron-up"></i>
                <i class="mfb-component__main-icon--active fas fa-chevron-up"></i>
            </a>
        </li>
    </ul>
*/
//Scroll to the top of the page function
$(function () {
    var myScrollTopContainer, name, arr;

    //Get the button container
    myScrollTopContainer = document.getElementById("scroll-topContainter");

    // When the user scrolls down 20px from the top of the document, show the button
    if (myScrollTopContainer != null) {
        window.onscroll = function () { scrollFunction() };
    }

    function scrollFunction() {
        if (document.body.scrollTop > 20 || document.documentElement.scrollTop > 20) {
            // The classList property is not supported in Internet Explorer 9. The following code will work in all browsers:
            myScrollTopContainer.className = myScrollTopContainer.className.replace(/\bd-none\b/g, "");
        } else {
            // The classList property is not supported in Internet Explorer 9. The following code will work in all browsers:
            name = "d-none";
            arr = myScrollTopContainer.className.split(" ");
            if (arr.indexOf(name) == -1) {
                myScrollTopContainer.className += " " + name;
            }
        }
    }

    // When the user clicks on the button, scroll to the top of the document
    $(`#scroll-topButton`).click(function () {
        document.body.scrollTop = 0;
        document.documentElement.scrollTop = 0;
    });
});