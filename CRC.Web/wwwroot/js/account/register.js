// @ts-nocheck
(function () {
    const btn = document.getElementById('btnRegister');
    const msg = document.getElementById('registerMessage');

    if (!btn) return;

    btn.addEventListener('click', async function () {
        msg.textContent = '';
        msg.style.color = '';

        const payload = {
            name: document.getElementById('Name').value.trim(),
            username: document.getElementById('Username').value.trim(),
            email: document.getElementById('Email').value.trim(),
            password: document.getElementById('Password').value,
            userType: parseInt(document.getElementById('UserType').value)
        };

        if (!payload.name || !payload.username || !payload.email || !payload.password) {
            msg.textContent = 'Please fill in all required fields.';
            msg.style.color = 'red';
            return;
        }

        try {
            const response = await fetch('/Account/RegisterUser', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json'
                },
                body: JSON.stringify(payload)
            });

            if (!response.ok) {
                msg.textContent = 'Server error during registration.';
                msg.style.color = 'red';
                return;
            }

            const result = await response.json();

            if (result.success) {
                msg.textContent = result.message || 'User registered successfully.';
                msg.style.color = 'green';

                // optional: clear fields
                document.getElementById('Name').value = '';
                document.getElementById('Username').value = '';
                document.getElementById('Email').value = '';
                document.getElementById('Password').value = '';
                document.getElementById('UserType').value = '1';
            } else {
                msg.textContent = result.message || 'Registration failed.';
                msg.style.color = 'red';
            }
        } catch (err) {
            console.error(err);
            msg.textContent = 'An unexpected error occurred.';
            msg.style.color = 'red';
        }
    });
})();