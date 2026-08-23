// Logout confirmation helper.
// Submits the logout form after the user confirms in the Blazor dialog.
// The logout must be a real HTTP POST (not Blazor SignalR) so the auth cookie
// can be cleared on the response — see AuthSessionController.LogoutAsync.
(function () {
  "use strict";

  window.DotNetCloudLogout = {
    submitLogoutForm: function () {
      var form = document.getElementById("logout-form");
      if (form) {
        form.submit();
        return true;
      }
      return false;
    },
  };
})();
