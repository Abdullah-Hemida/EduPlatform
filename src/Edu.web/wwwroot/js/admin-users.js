// wwwroot/js/admin-users.js
// Requires: jQuery, Bootstrap 5, DataTables (jquery.dataTables + dataTables.bootstrap5)
(function () {
    var antiToken = document.querySelector('meta[name="csrf-token"]')?.getAttribute('content') || '';

    // Localization helper (reads window.adminLocalization)
    function getLocalizedText(key, defaultValue) {
        if (window.adminLocalization && Object.prototype.hasOwnProperty.call(window.adminLocalization, key)) {
            return window.adminLocalization[key];
        }
        return defaultValue;
    }

    // small XSS helper
    function escapeHtml(s) {
        if (!s) return '';
        return s.replace(/[&<>"'`=\/]/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;', '/': '&#x2F;', '`': '&#x60;', '=': '&#3D;' }[c];
        });
    }

    // basic notification (prepend to card body)
    function showAlert(type, message) {
        var $card = $('#usersTable').closest('.card');
        var $container = $card.find('.card-body').first();
        var $a = $('<div class="alert alert-' + type + ' alert-dismissible fade show" role="alert"></div>');
        $a.text(message);
        $a.append('<button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>');
        $container.prepend($a);
        setTimeout(function () { $a.alert('close'); }, 4500);
    }

    // POST helper with antiforgery header
    function postJson(url, data, onSuccess, onError) {
        $.ajax({
            url: url,
            type: 'POST',
            headers: { 'RequestVerificationToken': antiToken },
            data: data,
            success: function (resp) {
                if (resp && resp.success) onSuccess && onSuccess(resp);
                else {
                    var msg = (resp && resp.message) ? resp.message : getLocalizedText('OperationFailed', 'Operation failed');
                    if (onError) onError(resp); else showAlert('danger', msg);
                }
            },
            error: function (xhr) {
                var text = xhr.responseText || xhr.statusText;
                if (onError) onError(xhr); else showAlert('danger', getLocalizedText('ServerError', 'Server error') + ': ' + text);
            }
        });
    }

    // Create single confirm modal if not present (for nicer UX than window.confirm)
    if ($('#adminConfirmModal').length === 0) {
        var modalHtml = ''
            + '<div class="modal fade" id="adminConfirmModal" tabindex="-1" aria-hidden="true">'
            + '  <div class="modal-dialog modal-dialog-centered">'
            + '    <div class="modal-content">'
            + '      <div class="modal-header"><h5 class="modal-title">' + getLocalizedText('Confirm', 'Confirm') + '</h5><button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button></div>'
            + '      <div class="modal-body"><p id="adminConfirmMessage">' + getLocalizedText('ConfirmDefault', 'Are you sure?') + '</p></div>'
            + '      <div class="modal-footer">'
            + '        <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">' + getLocalizedText('Cancel', 'Cancel') + '</button>'
            + '        <button type="button" id="adminConfirmOk" class="btn btn-primary">' + getLocalizedText('OK', 'OK') + '</button>'
            + '      </div>'
            + '    </div>'
            + '  </div>'
            + '</div>';
        $('body').append(modalHtml);
    }

    var confirmModalEl = document.getElementById('adminConfirmModal');
    var confirmModal = confirmModalEl ? new bootstrap.Modal(confirmModalEl, {}) : null;

    function confirmAction(message, callback) {
        // Use modal if available, else fallback to native confirm
        if (confirmModal) {
            $('#adminConfirmMessage').text(message || getLocalizedText('ConfirmDefault', 'Are you sure?'));
            $('#adminConfirmOk').off('click').on('click', function () {
                if (confirmModal) confirmModal.hide();
                callback && callback();
            });
            confirmModal.show();
        } else {
            if (window.confirm(message || getLocalizedText('ConfirmDefault', 'Are you sure?'))) {
                callback && callback();
            }
        }
    }

    // Attach modal/native confirm to forms with class "confirm"
    function attachConfirmToForms() {
        // delegate to document for dynamically added forms too.
        $(document).on('submit', 'form.confirm', function (e) {
            var $form = $(this);
            // If we already allowed this submit (flag), skip confirming
            if ($form.data('confirmed') === true) return true;

            e.preventDefault();
            var msg = $form.attr('data-confirm') || getLocalizedText('ConfirmDefault', 'Are you sure?');

            confirmAction(msg, function () {
                // mark as confirmed to avoid infinite loop
                $form.data('confirmed', true);

                // disable submit buttons to prevent double submits
                $form.find('button[type="submit"]').prop('disabled', true);

                // now submit the form
                $form.trigger('submit'); // will pass through since data('confirmed') now true
            });
        });
    }

    // Initialize DataTable only when the table provides a data-ajax-url attribute
    function initDataTable() {
        var $table = $('#usersTable');
        if (!$table.length) return;

        // Only initialize DT when data-ajax-url attribute is present (server-rendered table lacks it)
        var ajaxUrl = $table.attr('data-ajax-url');
        if (!ajaxUrl) {
            // no ajaxUrl -> server-rendered table; do not initialize DataTable
            return;
        }

        window.usersTable = $table.DataTable({
            processing: true,
            serverSide: true,
            ajax: {
                url: ajaxUrl,
                type: 'POST',
                headers: { 'RequestVerificationToken': antiToken },
                data: function (d) {
                    d.role = $('.role-filter.active').data('role') || 'All';
                    d.showDeleted = $('.deleted-filter.active').data('showdeleted') === true || $('.deleted-filter.active').data('showdeleted') === 'true';
                },
                error: function (xhr) {
                    console.error('DataTables Ajax error', xhr);
                }
            },
            columns: [
                {
                    data: null, orderable: false, searchable: false,
                    render: function (data, type, row, meta) {
                        return meta.row + meta.settings._iDisplayStart + 1;
                    }
                },
                {
                    data: 'photoUrl', orderable: false, searchable: false,
                    render: function (data) {
                        var src = data ? data : '/images/default-avatar.png';
                        return '<img src="' + escapeHtml(src) + '" class="rounded-circle" style="width:42px;height:42px;object-fit:cover" alt="">';
                    }
                },
                {
                    data: 'fullName', orderable: true, searchable: true,
                    render: function (data) {
                        var name = data || '—';
                        return '<div class="fw-semibold small text-truncate" style="max-width:220px;">' + escapeHtml(name) + '</div>';
                    }
                },
                {
                    data: 'phoneNumber', orderable: false, searchable: true,
                    render: function (data) { return data ? escapeHtml(data) : '<span class="text-muted">—</span>'; }
                },
                {
                    data: 'roles',
                    orderable: false,
                    searchable: false,
                    render: function (data, type, row) {
                        if (!data) return '';
                        var colors = { 'Admin': 'danger', 'Teacher': 'primary', 'Student': 'success' };
                        return data.split(',').map(function (r) {
                            return '<span class="badge bg-' + (colors[r.trim()] || 'secondary') + ' me-1">' + r.trim() + '</span>';
                        }).join(' ');
                    }
                },
                {
                    data: null, orderable: false, searchable: false,
                    render: function (data, type, row) {
                        var id = row.id;
                        var isTeacher = (row.roles || '').indexOf('Teacher') !== -1;
                        var deleted = row.isDeleted === true;
                        var hideDanger = (window.currentUserId && window.currentUserId === id);

                        var dropdown = '<div class="btn-group gap-1">'
                            + '<a class="btn btn-sm rounded btn-outline-primary" href="/Admin/ManageUsers/Details/' + id + '">' + getLocalizedText('View', 'View') + '</a>'
                            + '<button type="button" class="btn btn-sm rounded btn-outline-secondary dropdown-toggle dropdown-toggle-split" data-bs-toggle="dropdown" aria-expanded="false"></button>'
                            + '<ul class="dropdown-menu dropdown-menu-end">';

                        if (isTeacher) {
                            dropdown += '<li><a class="dropdown-item js-approve-teacher" href="#" data-id="' + id + '">' + getLocalizedText('Approve', 'Approve') + '</a></li>';
                            dropdown += '<li><a class="dropdown-item js-unapprove-teacher" href="#" data-id="' + id + '">' + getLocalizedText('Unapprove', 'Unapprove') + '</a></li>';
                            dropdown += '<li><hr class="dropdown-divider"></li>';
                        }

                        if (!hideDanger) {
                            if (deleted) {
                                dropdown += '<li><a class="dropdown-item js-soft-delete text-success" href="#" data-id="' + id + '">' + getLocalizedText('Restore', 'Restore') + '</a></li>';
                                dropdown += '<li><a class="dropdown-item text-danger js-hard-delete" href="#" data-id="' + id + '">' + getLocalizedText('DeletePermanently', 'Delete permanently') + '</a></li>';
                            } else {
                                dropdown += '<li><a class="dropdown-item text-danger js-soft-delete" href="#" data-id="' + id + '">' + getLocalizedText('Suspend', 'Suspend') + '</a></li>';
                            }
                        } else {
                            dropdown += '<li><span class="dropdown-item text-muted">' + getLocalizedText('NoActions', 'No actions') + '</span></li>';
                        }

                        dropdown += '</ul></div>';
                        return dropdown;
                    }
                }
            ],
            order: [[2, 'asc']],
            lengthMenu: [10, 20, 50],
            pageLength: 20,
            language: window.adminDtLang || {},
            createdRow: function (rowEl, rowData) {
                if (rowData.isDeleted) $(rowEl).addClass('table-secondary'); else $(rowEl).removeClass('table-secondary');
            }
        });

        // role filter handlers for DataTables UI
        $(document).on('click', '.role-filter', function (e) {
            e.preventDefault();
            $('.role-filter').removeClass('active');
            $(this).addClass('active');
            if (window.usersTable) window.usersTable.ajax.reload(null, false);
        });

        $(document).on('click', '.deleted-filter', function (e) {
            e.preventDefault();
            var $btn = $(this);
            var showDeleted = $btn.data('showdeleted') === true || $btn.data('showdeleted') === 'true';
            $('.deleted-filter').removeClass('active');
            $btn.addClass('active');
            $('.role-filter').removeClass('active').filter('[data-role="All"]').addClass('active');
            var statusText = showDeleted ? getLocalizedText('ViewingDeleted', 'Viewing: Deleted users') : getLocalizedText('ViewingActive', 'Viewing: Active users');
            $('#deletedStatusBadge').text(statusText);
            if (window.usersTable && window.usersTable.ajax) window.usersTable.ajax.reload(null, false);
        });

        // Action handlers (delegated) for DataTables UI (AJAX)
        $(document).on('click', '.js-approve-teacher', function (e) {
            e.preventDefault();
            var id = $(this).data('id'); if (!id) return;
            confirmAction(getLocalizedText('ConfirmApprove', 'Approve this teacher?'), function () {
                postJson('/Admin/ManageUsers/ApproveTeacherJson', { id: id }, function (resp) {
                    showAlert('success', resp.message);
                    if (window.usersTable) window.usersTable.ajax.reload(null, false);
                });
            });
        });

        $(document).on('click', '.js-unapprove-teacher', function (e) {
            e.preventDefault();
            var id = $(this).data('id'); if (!id) return;
            confirmAction(getLocalizedText('ConfirmUnapprove', 'Unapprove this teacher?'), function () {
                postJson('/Admin/ManageUsers/UnapproveTeacherJson', { id: id }, function (resp) {
                    showAlert('success', resp.message);
                    if (window.usersTable) window.usersTable.ajax.reload(null, false);
                });
            });
        });

        $(document).on('click', '.js-soft-delete', function (e) {
            e.preventDefault();
            var id = $(this).data('id'); if (!id) return;
            var row = window.usersTable.row($(this).closest('tr')).data();
            var isRestore = row && row.isDeleted === true;
            var msg = isRestore ? getLocalizedText('ConfirmRestore', 'Restore this user?') : getLocalizedText('ConfirmSuspend', 'Suspend this user?');
            confirmAction(msg, function () {
                postJson('/Admin/ManageUsers/SoftDeleteJson', { id: id }, function (resp) {
                    showAlert('success', resp.message);
                    if (window.usersTable) window.usersTable.ajax.reload(null, false);
                });
            });
        });

        $(document).on('click', '.js-hard-delete', function (e) {
            e.preventDefault();
            var id = $(this).data('id'); if (!id) return;
            confirmAction(getLocalizedText('ConfirmDelete', 'This will permanently delete the user and cannot be undone. Proceed?'), function () {
                postJson('/Admin/ManageUsers/HardDeleteJson', { id: id }, function (resp) {
                    showAlert('success', resp.message);
                    if (window.usersTable) window.usersTable.ajax.reload(null, false);
                }, function (err) {
                    showAlert('danger', (err && err.message) ? err.message : getLocalizedText('OperationFailed', 'Failed to delete'));
                });
            });
        });
    }

    // initialize behaviors on DOM ready
    $(function () {
        // attach confirm behavior to forms (both server-rendered and JS-enhanced)
        attachConfirmToForms();

        // init DataTable only when table expects ajax (data-ajax-url attribute)
        initDataTable();
    });

    // expose for debugging
    window.adminUsers = { initDataTable: initDataTable, attachConfirmToForms: attachConfirmToForms };
})();
