// @ts-nocheck
(function () {
    const branchTableBody = document.querySelector('#assetBranchTable tbody');
    const assetTableBody = document.querySelector('#assetTable tbody');

    const btnAddAsset = document.getElementById('btnAddAsset');
    const branchTitle = document.getElementById('assetBranchTitle');

    const modalEl = document.getElementById('assetModal');
    const modal = modalEl ? new bootstrap.Modal(modalEl) : null;

    const hiddenIsNew = document.getElementById('AssetIsNew');
    const hiddenBranchId = document.getElementById('AssetBranchId');
    const txtBranchName = document.getElementById('AssetBranchName');

    const txtAssetId = document.getElementById('AssetId');
    const txtAssetName = document.getElementById('AssetName');
    const txtQuantity = document.getElementById('AssetQuantity');
    const txtCost = document.getElementById('AssetCost');
    const msg = document.getElementById('assetFormMessage');
    const btnSave = document.getElementById('btnSaveAsset');

    let selectedBranchId = null;
    let selectedBranchName = null;

    function clearAssetForm() {
        if (!txtAssetId) return;

        txtAssetId.value = '';
        txtAssetName.value = '';
        txtQuantity.value = '';
        txtCost.value = '';
        if (hiddenIsNew) hiddenIsNew.value = 'true';
        if (msg) {
            msg.textContent = '';
            msg.classList.remove('text-success');
            msg.classList.add('text-danger');
        }
        const label = document.getElementById('assetModalLabel');
        if (label) label.textContent = 'Add Asset';
    }

    function setSelectedBranch(branchId, branchName) {
        selectedBranchId = branchId;
        selectedBranchName = branchName;

        if (branchTitle) {
            branchTitle.textContent = `Assets for ${branchName} (${branchId})`;
        }

        if (btnAddAsset) {
            btnAddAsset.disabled = false;
        }
    }

    async function loadActiveBranches() {
        if (!branchTableBody) return;

        branchTableBody.innerHTML = '<tr><td colspan="2">Loading branches...</td></tr>';

        try {
            const response = await fetch('/Asset/GetActiveBranches', {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                branchTableBody.innerHTML = '<tr><td colspan="2">Error loading branches.</td></tr>';
                return;
            }

            const data = await response.json();

            if (!data || data.length === 0) {
                branchTableBody.innerHTML = '<tr><td colspan="2">No active branches.</td></tr>';
                return;
            }

            branchTableBody.innerHTML = '';

            data.forEach((b, index) => {
                const tr = document.createElement('tr');
                tr.setAttribute('data-branch-id', b.branchId);
                tr.setAttribute('data-branch-name', b.branchName);
                tr.classList.add('branch-row');

                tr.innerHTML = `
                    <td>${b.branchId}</td>
                    <td>${b.branchName ?? ''}</td>
                `;

                branchTableBody.appendChild(tr);

                // auto-select first branch
                if (index === 0) {
                    selectBranchRow(tr);
                }
            });
        } catch (err) {
            console.error(err);
            branchTableBody.innerHTML = '<tr><td colspan="2">Error loading branches.</td></tr>';
        }
    }

    function selectBranchRow(row) {
        // remove previous selection
        const old = branchTableBody.querySelectorAll('.table-active');
        old.forEach(r => r.classList.remove('table-active'));

        row.classList.add('table-active');

        const branchId = row.getAttribute('data-branch-id');
        const branchName = row.getAttribute('data-branch-name');

        setSelectedBranch(branchId, branchName);
        loadAssets(branchId);
    }

    async function loadAssets(branchId) {
        if (!assetTableBody) return;

        if (!branchId) {
            assetTableBody.innerHTML = '<tr><td colspan="6">Select a branch to view assets.</td></tr>';
            return;
        }

        assetTableBody.innerHTML = '<tr><td colspan="6">Loading assets...</td></tr>';

        try {
            const response = await fetch('/Asset/GetAssets?branchId=' + encodeURIComponent(branchId), {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                assetTableBody.innerHTML = '<tr><td colspan="6">Error loading assets.</td></tr>';
                return;
            }

            const result = await response.json();

            if (!result.success || !result.data || result.data.length === 0) {
                assetTableBody.innerHTML = '<tr><td colspan="6">No assets found for this branch.</td></tr>';
                return;
            }

            const data = result.data;
            assetTableBody.innerHTML = '';

            data.forEach(a => {
                const tr = document.createElement('tr');
                tr.setAttribute('data-asset-id', a.assetId);

                const cost = a.cost ?? 0;
                const totalCost = a.totalCost ?? 0;

                tr.innerHTML = `
                    <td>${a.assetId}</td>
                    <td>${a.name ?? ''}</td>
                    <td>${a.quantity ?? 0}</td>
                    <td>${cost.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</td>
                    <td>${totalCost.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</td>
                    <td>
                    <button type="button"
                class="btn btn-sm btn-secondary btn-asset-edit"
                data-id="${a.assetId}"
                title="Edit">
            <i class="fas fa-edit"></i>
        </button>
        <button type="button"
                class="btn btn-sm btn-danger btn-asset-delete ms-1"
                data-id="${a.assetId}"
                title="Delete">
            <i class="fas fa-trash"></i>
        </button>                    </td>
                `;

                assetTableBody.appendChild(tr);
            });

        } catch (err) {
            console.error(err);
            assetTableBody.innerHTML = '<tr><td colspan="6">Error loading assets.</td></tr>';
        }
    }

    function openAddAssetModal() {
        clearAssetForm();

        if (!selectedBranchId || !selectedBranchName) {
            if (msg) msg.textContent = 'Please select a branch first.';
            return;
        }

        if (hiddenBranchId) hiddenBranchId.value = selectedBranchId;
        if (txtBranchName) txtBranchName.value = `${selectedBranchName} (${selectedBranchId})`;

        const label = document.getElementById('assetModalLabel');
        if (label) label.textContent = 'Add Asset';

        if (modal) modal.show();
    }

    async function openEditAssetModal(assetId) {
        clearAssetForm();

        const label = document.getElementById('assetModalLabel');
        if (label) label.textContent = 'Edit Asset';

        try {
            const response = await fetch('/Asset/GetAsset?assetId=' + encodeURIComponent(assetId), {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                if (msg) msg.textContent = 'Error loading asset details.';
                if (modal) modal.show();
                return;
            }

            const result = await response.json();

            if (!result.success) {
                if (msg) msg.textContent = result.message || 'Asset not found.';
                if (modal) modal.show();
                return;
            }

            const a = result.data;

            if (hiddenIsNew) hiddenIsNew.value = 'false';
            if (hiddenBranchId) hiddenBranchId.value = a.branchId || '';
            if (txtBranchName) txtBranchName.value = `${a.branchName} (${a.branchId})`;

            txtAssetId.value = a.assetId || '';
            txtAssetName.value = a.name || '';
            txtQuantity.value = a.quantity ?? 0;
            txtCost.value = a.cost ?? 0;

            if (modal) modal.show();
        } catch (err) {
            console.error(err);
            if (msg) msg.textContent = 'Error loading asset details.';
            if (modal) modal.show();
        }
    }

    async function saveAsset() {
        if (!txtAssetId) return;

        if (msg) {
            msg.textContent = '';
            msg.classList.remove('text-success');
            msg.classList.add('text-danger');
        }

        const isNew = hiddenIsNew && hiddenIsNew.value === 'true';

        const branchId = hiddenBranchId ? hiddenBranchId.value : '';
        const branchName = txtBranchName ? txtBranchName.value : '';

        // Extract pure branch name from "Name (ID)" if needed
        let cleanBranchName = branchName;
        const idx = branchName.lastIndexOf('(');
        if (idx > 0) {
            cleanBranchName = branchName.substring(0, idx).trim();
        }

        const quantityVal = parseInt(txtQuantity.value || '0', 10);
        const costVal = parseFloat(txtCost.value || '0');

        const payload = {
            isNew: isNew,
            assetId: txtAssetId.value.trim(),
            name: txtAssetName.value.trim(),
            branchId: branchId,
            branchName: cleanBranchName,
            quantity: quantityVal,
            cost: costVal
        };

        if (!payload.assetId || !payload.name || !payload.branchId || payload.quantity <= 0 || payload.cost < 0) {
            if (msg) msg.textContent = 'Please fill in all required fields and ensure quantity/cost are valid.';
            return;
        }

        try {
            const response = await fetch('/Asset/SaveAsset', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json'
                },
                body: JSON.stringify(payload)
            });

            if (!response.ok) {
                if (msg) msg.textContent = 'Server error while saving asset.';
                return;
            }

            const result = await response.json();

            if (!result.success) {
                if (msg) msg.textContent = result.message || 'Failed to save asset.';
                return;
            }

            if (msg) msg.textContent = '';
            if (modal) modal.hide();

            if (selectedBranchId) {
                loadAssets(selectedBranchId);
            }
        } catch (err) {
            console.error(err);
            if (msg) msg.textContent = 'An unexpected error occurred.';
        }
    }

    async function deleteAsset(assetId) {
        if (!confirm('Are you sure you want to delete this asset?')) {
            return;
        }

        try {
            const response = await fetch('/Asset/DeleteAsset', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json'
                },
                body: JSON.stringify({ assetId: assetId })
            });

            if (!response.ok) {
                alert('Server error while deleting asset.');
                return;
            }

            const result = await response.json();

            if (!result.success) {
                alert(result.message || 'Failed to delete asset.');
                return;
            }

            if (selectedBranchId) {
                loadAssets(selectedBranchId);
            }
        } catch (err) {
            console.error(err);
            alert('An unexpected error occurred.');
        }
    }

    function attachHandlers() {
        if (branchTableBody) {
            branchTableBody.addEventListener('click', function (e) {
                let row = e.target.closest('tr.branch-row');
                if (!row) return;
                selectBranchRow(row);
            });
        }

        document.addEventListener('click', function (e) {
            const target = e.target;

            const editBtn = target.closest('.btn-asset-edit');
            if (editBtn) {
                const id = editBtn.getAttribute('data-id');
                if (id) openEditAssetModal(id);
            }

            const deleteBtn = target.closest('.btn-asset-delete');
            if (deleteBtn) {
                const id = deleteBtn.getAttribute('data-id');
                if (id) deleteAsset(id);
            }
        });

        if (btnAddAsset) {
            btnAddAsset.addEventListener('click', openAddAssetModal);
        }

        if (btnSave) {
            btnSave.addEventListener('click', saveAsset);
        }
    }

    document.addEventListener('DOMContentLoaded', function () {
        loadActiveBranches();
        attachHandlers();
        assetTableBody.innerHTML = '<tr><td colspan="6">Select a branch to view assets.</td></tr>';
    });
})();