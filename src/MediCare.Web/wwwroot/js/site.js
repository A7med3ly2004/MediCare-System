// jQuery Validation adapters for custom attributes (MustBeTrue, etc.)
if (typeof jQuery !== 'undefined' && jQuery.validator) {
    // Adapter for [MustBeTrue]
    jQuery.validator.addMethod("mustbetrue", function (value, element) {
        return element.checked;
    });
    if (jQuery.validator.unobtrusive) {
        jQuery.validator.unobtrusive.adapters.addBool("mustbetrue");
    }

    // Defense-in-depth: patch range validator for checkboxes
    var origRange = jQuery.validator.methods.range;
    jQuery.validator.methods.range = function (value, element, param) {
        if (element.type === "checkbox") {
            return element.checked;
        }
        return origRange.call(this, value, element, param);
    };
}

// MediCare Client-side Interactions
document.addEventListener('DOMContentLoaded', function () {
    const sidebar = document.getElementById('mcSidebar');
    const overlay = document.querySelector('.mc-sidebar-overlay');
    const hamburger = document.querySelector('.mc-hamburger');

    if (sidebar && overlay) {
        if (hamburger) {
            hamburger.addEventListener('click', function () {
                const isOpen = sidebar.classList.toggle('open');
                overlay.classList.toggle('open', isOpen);
            });
        }

        overlay.addEventListener('click', function () {
            sidebar.classList.remove('open');
            overlay.classList.remove('open');
        });

        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && sidebar.classList.contains('open')) {
                sidebar.classList.remove('open');
                overlay.classList.remove('open');
            }
        });
    }

    // Auto-dismiss alerts/toasts with data-bs-dismiss="alert" after 6 seconds if present
    const alerts = document.querySelectorAll('.alert-dismissible');
    alerts.forEach(function (alert) {
        setTimeout(function () {
            try {
                const bsAlert = bootstrap.Alert.getOrCreateInstance(alert);
                bsAlert.close();
            } catch (e) {
                // Ignore if bootstrap is not yet loaded
            }
        }, 6000);
    });
});
