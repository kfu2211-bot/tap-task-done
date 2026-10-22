document.addEventListener("DOMContentLoaded", function () {

    // =========================================
    // PASSWORD POPUP + PASSWORD STRENGTH
    // =========================================

    const passwordInput =
        document.getElementById("password");

    const passwordPopup =
        document.getElementById("passwordPopup");

    const strengthText =
        document.getElementById("strengthText");

    const ruleLength =
        document.getElementById("ruleLength");

    const ruleUpper =
        document.getElementById("ruleUpper");

    const ruleLower =
        document.getElementById("ruleLower");

    const ruleNumber =
        document.getElementById("ruleNumber");

    const ruleSpecial =
        document.getElementById("ruleSpecial");


    const commonPasswords = [

        "password",
        "password123",
        "123456",
        "12345678",
        "123456789",
        "1234567890",
        "qwerty",
        "qwerty123",
        "admin",
        "admin123",
        "letmein",
        "welcome",
        "iloveyou",
        "abc123"

    ];


    // =========================================
    // SHOW POPUP WHEN PASSWORD IS CLICKED
    // =========================================

    if (passwordInput && passwordPopup) {

        passwordInput.addEventListener(
            "focus",
            function () {

                passwordPopup.classList.add("show");

                checkPasswordStrength(
                    passwordInput.value
                );

            }
        );


        // =====================================
        // HIDE POPUP WHEN LEAVING PASSWORD
        // =====================================

        passwordInput.addEventListener(
            "blur",
            function () {

                setTimeout(function () {

                    if (
                        document.activeElement !== passwordInput
                    ) {

                        passwordPopup.classList.remove(
                            "show"
                        );

                    }

                }, 100);

            }
        );


        // =====================================
        // CHECK WHILE TYPING
        // =====================================

        passwordInput.addEventListener(
            "input",
            function () {

                checkPasswordStrength(
                    passwordInput.value
                );

            }
        );

    }


    // =========================================
    // PASSWORD CHECKER
    // =========================================

    function checkPasswordStrength(password) {

        const lengthOK =
            password.length >= 8;

        const upperOK =
            /[A-Z]/.test(password);

        const lowerOK =
            /[a-z]/.test(password);

        const numberOK =
            /[0-9]/.test(password);

        const specialOK =
            /[^A-Za-z0-9]/.test(password);


        const commonOK =
            !commonPasswords.includes(
                password.toLowerCase()
            );


        // =====================================
        // UPDATE RULES
        // =====================================

        updateRule(
            ruleLength,
            lengthOK
        );

        updateRule(
            ruleUpper,
            upperOK
        );

        updateRule(
            ruleLower,
            lowerOK
        );

        updateRule(
            ruleNumber,
            numberOK
        );

        updateRule(
            ruleSpecial,
            specialOK
        );


        // =====================================
        // EMPTY
        // =====================================

        if (password.length === 0) {

            if (strengthText) {

                strengthText.textContent =
                    "Enter a strong password";

            }

            return false;

        }


        // =====================================
        // COMMON PASSWORD
        // =====================================

        if (!commonOK) {

            if (strengthText) {

                strengthText.textContent =
                    "Too common — choose a stronger password";

            }

            return false;

        }


        // =====================================
        // SCORE
        // =====================================

        let score = 0;


        if (lengthOK) score++;

        if (upperOK) score++;

        if (lowerOK) score++;

        if (numberOK) score++;

        if (specialOK) score++;


        if (score <= 2) {

            strengthText.textContent =
                "Weak password";

            return false;

        }


        if (score === 3) {

            strengthText.textContent =
                "Medium password";

            return false;

        }


        if (score === 4) {

            strengthText.textContent =
                "Strong password";

            return false;

        }


        if (score === 5) {

            strengthText.textContent =
                "Very strong password";

            return true;

        }


        return false;

    }


    // =========================================
    // UPDATE PASSWORD RULE
    // =========================================

    function updateRule(element, valid) {

        if (!element) {
            return;
        }


        const icon =
            element.querySelector("span");


        if (!icon) {
            return;
        }


        if (valid) {

            element.classList.add("valid");

            icon.textContent = "✓";

        }
        else {

            element.classList.remove("valid");

            icon.textContent = "○";

        }

    }

    /* =========================================
       CUSTOM DROPDOWNS
    ========================================= */

    const monthDropdown =
        document.getElementById("monthDropdown");

    const dayDropdown =
        document.getElementById("dayDropdown");

    const yearDropdown =
        document.getElementById("yearDropdown");


    let selectedMonth = document.getElementById("birthMonth")?.value || "";

    let selectedDay = document.getElementById("birthDay")?.value || "";

    let selectedYear = document.getElementById("birthYear")?.value || "";


    /* =========================================
       YEAR OPTIONS
    ========================================= */

    const yearOptions =
        document.getElementById("yearOptions");

    const currentYear =
        new Date().getFullYear();


    if (yearOptions) {
        for (
            let year = currentYear;
            year >= 1900;
            year--
        ) {

            const option =
                document.createElement("div");

            option.className =
                "dropdown-option";

            option.dataset.value =
                year;

            option.textContent =
                year;

            yearOptions.appendChild(option);
        }
    }


    /* =========================================
       DROPDOWN FUNCTION
    ========================================= */

    function setupDropdown(dropdown, callback) {

        if (!dropdown) {
            return;
        }

        const button =
            dropdown.querySelector(".dropdown-button");

        const menu =
            dropdown.querySelector(".dropdown-menu");

        const buttonText =
            button.querySelector("span");


        button.addEventListener(
            "click",
            function (event) {

                event.stopPropagation();

                // Close other dropdowns

                document
                    .querySelectorAll(".custom-dropdown")
                    .forEach(function (other) {

                        if (other !== dropdown) {

                            other.classList.remove("active");

                        }

                    });


                dropdown.classList.toggle("active");

            }
        );


        menu.addEventListener(
            "click",
            function (event) {

                const option =
                    event.target.closest(
                        ".dropdown-option"
                    );


                if (!option) {
                    return;
                }


                buttonText.textContent =
                    option.textContent;


                menu
                    .querySelectorAll(".dropdown-option")
                    .forEach(function (item) {

                        item.classList.remove(
                            "selected"
                        );

                    });


                option.classList.add("selected");


                dropdown.classList.remove(
                    "active"
                );


                callback(
                    option.dataset.value
                );

            }
        );

    }


    /* =========================================
       MONTH
    ========================================= */

    setupDropdown(
        monthDropdown,
        function (value) {

            selectedMonth =
                value;

            document.getElementById("birthMonth").value = value;

            updateDays();

        }
    );


    /* =========================================
       DAY
    ========================================= */

    setupDropdown(
        dayDropdown,
        function (value) {

            selectedDay =
                value;

            document.getElementById("birthDay").value = value;

        }
    );


    /* =========================================
       YEAR
    ========================================= */

    setupDropdown(
        yearDropdown,
        function (value) {

            selectedYear =
                value;

            document.getElementById("birthYear").value = value;

            updateDays();

        }
    );


    /* =========================================
       DAYS
    ========================================= */

    function updateDays() {

        const dayMenu =
            dayDropdown.querySelector(
                ".dropdown-menu"
            );


        dayMenu.innerHTML = "";


        let days = 31;


        if (
            selectedMonth == 4 ||
            selectedMonth == 6 ||
            selectedMonth == 9 ||
            selectedMonth == 11
        ) {

            days = 30;

        }


        if (selectedMonth == 2) {

            if (
                !selectedYear ||
                (
                    selectedYear % 400 === 0 ||
                    (
                        selectedYear % 4 === 0 &&
                        selectedYear % 100 !== 0
                    )
                )
            ) {

                days = 29;

            } else {

                days = 28;

            }

        }


        for (
            let day = 1;
            day <= days;
            day++
        ) {

            const option =
                document.createElement("div");


            option.className =
                "dropdown-option";


            option.dataset.value =
                day;


            option.textContent =
                day;


            dayMenu.appendChild(
                option
            );

        }


        // Preserve the selected day unless it is invalid for the new month or year.
        if (selectedDay && Number(selectedDay) > days) {
            selectedDay = "";
            document.getElementById("birthDay").value = "";
        }
        dayDropdown.querySelector(".dropdown-button span").textContent = selectedDay || "Day";

    }

    if (selectedMonth && selectedYear) updateDays();
    if (selectedMonth) monthDropdown.querySelector(".dropdown-button span").textContent = new Date(2000, Number(selectedMonth) - 1, 1).toLocaleString("en", { month: "long" });
    if (selectedDay) dayDropdown.querySelector(".dropdown-button span").textContent = selectedDay;
    if (selectedYear) yearDropdown.querySelector(".dropdown-button span").textContent = selectedYear;

    document.getElementById("registerForm")?.addEventListener("submit", function (event) {
        if (!selectedMonth || !selectedDay || !selectedYear) {
            event.preventDefault();
            alert("Please select your complete date of birth.");
        }
    });


    /* =========================================
       CLOSE WHEN CLICKING OUTSIDE
    ========================================= */

    document.addEventListener(
        "click",
        function () {

            document
                .querySelectorAll(
                    ".custom-dropdown"
                )
                .forEach(function (dropdown) {

                    dropdown.classList.remove(
                        "active"
                    );

                });

        }
    );


});
