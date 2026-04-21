namespace FMO.IO.AMAC.JsonModels;

using System.Collections.Generic;

public class EmployeeRoot
{
    public int total { get; set; }
    public List<Employee>? list { get; set; }
    public int pageNum { get; set; }
    public int pageSize { get; set; }
    public int size { get; set; }
    public int startRow { get; set; }
    public int endRow { get; set; }
    public int pages { get; set; }
    public int prePage { get; set; }
    public int nextPage { get; set; }
    public bool isFirstPage { get; set; }
    public bool isLastPage { get; set; }
    public bool hasPreviousPage { get; set; }
    public bool hasNextPage { get; set; }
    public int navigatePages { get; set; }
    public int navigateFirstPage { get; set; }
    public int navigateLastPage { get; set; }
}

public class Employee
{
    public long id { get; set; }
    public long creationDate { get; set; }
    public bool isMasking { get; set; }
    public long accountId { get; set; }
    public string? username { get; set; }
    public string? idType { get; set; }
    public string? idNumber { get; set; }
    public string? email { get; set; }
    public string? mobile { get; set; }
    public string? station { get; set; }
    public string? qualification { get; set; } 
    public string? state { get; set; }
    public long registerDate { get; set; }
    public long submitDate { get; set; }
    public long? completeDate { get; set; }
    public string? nationality { get; set; } 
    public string? branchCode { get; set; } 
    public long userId { get; set; }
    public long orgId { get; set; }
    public string? bizs { get; set; } 
    public string? registerStatus { get; set; } // 注意：\u0000 可能需要特殊处理 
    public string? orgName { get; set; }

    public string? rootOrgName { get; set; }
    public long parentId { get; set; }  
    public string? certCode { get; set; }
    public bool partTimePE { get; set; }
    public bool partTimeStatus { get; set; }
    public bool executivesStatus { get; set; }
    public string? post { get; set; }
    public long employTime { get; set; } 
    public bool disHasPartAndExecutives { get; set; } 
    public string? confirmPhone { get; set; } // 注意：部分值为 null
}