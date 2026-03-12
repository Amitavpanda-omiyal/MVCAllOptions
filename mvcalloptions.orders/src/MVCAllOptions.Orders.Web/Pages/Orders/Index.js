$(function () {
    var l = abp.localization.getResource('Orders');
    var createModal = new abp.ModalManager(abp.appPath + 'Orders/CreateModal');

    var dataTable = $('#OrdersTable').DataTable(
        abp.libs.datatables.normalizeConfiguration({
            serverSide: true,
            paging: true,
            order: [[0, "asc"]],
            searching: false,
            ajax: abp.libs.datatables.createAjax(mVCAllOptions.orders.order.getList),
            columnDefs: [
                {
                    title: l('CustomerName'),
                    data: "customerName"
                },
                {
                    title: l('BookName'),
                    data: "bookName"
                },
                {
                    title: l('State'),
                    data: "state",
                    render: function (data) {
                        return l('Enum:OrderState.' + data);
                    }
                },
                {
                    title: l('CreationTime'),
                    data: "creationTime",
                    dataFormat: "datetime"
                }
            ]
        })
    );

    createModal.onResult(function () {
        abp.notify.success(l('CreatedSuccessfully'));
        dataTable.ajax.reload();
    });

    $('#NewOrderButton').click(function (e) {
        e.preventDefault();
        createModal.open();
    });
});
