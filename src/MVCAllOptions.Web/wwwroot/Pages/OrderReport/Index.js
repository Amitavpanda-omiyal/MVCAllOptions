$(function () {
    var l = abp.localization.getResource('MVCAllOptions');

    var dataTable = $('#OrderReportTable').DataTable(
        abp.libs.datatables.normalizeConfiguration({
            serverSide: false,
            paging: true,
            order: [[3, 'desc']],
            searching: true,
            ajax: abp.libs.datatables.createAjax(
                mVCAllOptions.reports.orderReport.getLatestOrders
            ),
            columnDefs: [
                {
                    title: l('CustomerName'),
                    data: 'customerName'
                },
                {
                    title: l('BookName'),
                    data: 'bookName'
                },
                {
                    title: l('State'),
                    data: 'state',
                    render: function (data) {
                        var states = { 0: 'Placed', 1: 'Delivered', 2: 'Canceled' };
                        return states[data] || data;
                    }
                },
                {
                    title: l('CreationTime'),
                    data: 'creationTime',
                    dataFormat: 'datetime'
                },
                {
                    title: l('BookId'),
                    data: 'bookId',
                    visible: false
                }
            ]
        })
    );
});
