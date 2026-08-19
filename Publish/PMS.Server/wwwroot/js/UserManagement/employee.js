function fetchEmployeeData() {
    var empNo = document.getElementById("EmpFileNo").value;
    if (!empNo) return;

    fetch('/UserManagement/GetEmployeeData?empNo=' + empNo)
        .then(response => response.json())
        .then(data => {
            if (data.exists) {
                showAlert("المستخدم موجود مسبقًا!", "danger");
                document.getElementById("EmpFileNo").readOnly = false;
                return;
            }

            document.getElementById("EmpFileNo").readOnly = true;
            document.getElementById("EmpName").value = data.empName || "";
            document.getElementById("Department").value = data.perRespCodeNoName || "";
            document.getElementById("JobTypeName").value = data.jobtypeName || "";
            document.getElementById("EmpEmail").value = data.empEmail || "";
            document.getElementById("JobStatus").value = data.jobStatus || "";

            document.getElementById("EmpPhone").value = data.empPhone || "";
            document.getElementById("RespCodeId").value = parseInt(data.respCodeId) || 0;
            document.getElementById("EmpResponsibilityCode").value = data.empResponsibilitycode || "";
            document.getElementById("JobCatId").value = data.jobCatId || 0;
            document.getElementById("DesignationId").value = data.designationId || 0;
        })
        .catch(error => console.error('Error:', error));
}
