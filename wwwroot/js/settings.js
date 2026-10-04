document.addEventListener("DOMContentLoaded", function () {
    const keysToReset = ["tapTaskDoneTasks", "tapTaskDoneCurrentTask", "tapTaskDoneSelectedTaskId", "tapTaskDoneTrackingStatus", "tapTaskDonePendingRating", "tapTaskDoneReviews", "tapTaskDoneReview", "tapTaskDoneReports", "tapTaskDoneNotifications", "tapTaskDoneMessages"];
    const form = document.getElementById("profileForm");
    const message = document.getElementById("profileSavedMessage");
    const photoInput = document.getElementById("profilePhotoInput");
    const photoMessage = document.getElementById("photoMessage");
    let account = {};
    try { account = JSON.parse(localStorage.getItem("tapTaskDoneAccount") || "{}") || {}; } catch (error) { account = {}; }
    function updateProfileSummary() {
        const name = account.nickname || account.name || "User";
        const email = account.email || "user@email.com";
        document.getElementById("profileSummaryName").textContent = name;
        document.getElementById("profileSummaryEmail").textContent = email;
        const avatar = document.getElementById("profileAvatar");
        const photo = account.profilePhoto || "";
        avatar.replaceChildren();
        if (photo) {
            const image = document.createElement("img");
            image.src = photo;
            image.alt = "Profile photo";
            avatar.appendChild(image);
            avatar.classList.add("has-photo");
        } else {
            avatar.textContent = name.trim().charAt(0).toUpperCase() || "U";
            avatar.classList.remove("has-photo");
        }
    }
    function compressPhoto(file) {
        return new Promise(function (resolve, reject) {
            const reader = new FileReader();
            reader.onerror = function () { reject(new Error("Unable to read this image.")); };
            reader.onload = function () {
                const image = new Image();
                image.onerror = function () { reject(new Error("This image cannot be used.")); };
                image.onload = function () {
                    const limit = 480;
                    const scale = Math.min(1, limit / Math.max(image.width, image.height));
                    const canvas = document.createElement("canvas");
                    canvas.width = Math.max(1, Math.round(image.width * scale));
                    canvas.height = Math.max(1, Math.round(image.height * scale));
                    const context = canvas.getContext("2d");
                    context.drawImage(image, 0, 0, canvas.width, canvas.height);
                    resolve(canvas.toDataURL("image/jpeg", 0.86));
                };
                image.src = reader.result;
            };
            reader.readAsDataURL(file);
        });
    }
    document.getElementById("profileNameInput").value = account.nickname || account.name || "";
    document.getElementById("profileEmailInput").value = account.email || "";
    document.getElementById("profileServiceInput").value = account.service || account.profession || "";
    document.getElementById("profileRoleInput").value = account.role || "client";
    document.getElementById("profileAvailabilityInput").value = account.availability || "Available";
    updateProfileSummary();
    photoInput.addEventListener("change", function () {
        const file = photoInput.files && photoInput.files[0];
        photoMessage.textContent = "";
        if (!file) return;
        if (!["image/jpeg", "image/png", "image/webp"].includes(file.type)) { photoMessage.textContent = "Choose a JPG, PNG, or WEBP image."; photoInput.value = ""; return; }
        if (file.size > 5 * 1024 * 1024) { photoMessage.textContent = "Choose an image smaller than 5 MB."; photoInput.value = ""; return; }
        compressPhoto(file).then(function (photo) {
            account.profilePhoto = photo;
            try { localStorage.setItem("tapTaskDoneAccount", JSON.stringify(account)); }
            catch (error) { delete account.profilePhoto; photoMessage.textContent = "This photo is too large to save on this device."; return; }
            updateProfileSummary();
            photoMessage.textContent = "Profile photo updated locally.";
            photoInput.value = "";
        }).catch(function (error) { photoMessage.textContent = error.message; photoInput.value = ""; });
    });
    document.getElementById("removePhotoButton").addEventListener("click", function () {
        if (!account.profilePhoto) { photoMessage.textContent = "No profile photo to remove."; return; }
        delete account.profilePhoto;
        localStorage.setItem("tapTaskDoneAccount", JSON.stringify(account));
        updateProfileSummary();
        photoMessage.textContent = "Profile photo removed.";
    });
    form.addEventListener("submit", function (event) { event.preventDefault(); account.name = document.getElementById("profileNameInput").value.trim(); account.nickname = account.name; account.email = document.getElementById("profileEmailInput").value.trim(); account.service = document.getElementById("profileServiceInput").value.trim() || "Freelancer"; account.role = document.getElementById("profileRoleInput").value; account.availability = document.getElementById("profileAvailabilityInput").value; localStorage.setItem("tapTaskDoneAccount", JSON.stringify(account)); updateProfileSummary(); message.textContent = "Changes saved locally."; });
    document.getElementById("resetDemoButton").addEventListener("click", function () { if (!window.confirm("Reset all local demo tasks, reviews, reports, messages, and notifications?")) return; keysToReset.forEach(function (key) { localStorage.removeItem(key); }); sessionStorage.removeItem("tapTaskDoneShowRating"); window.location.href = "/Dashboard"; });
});
