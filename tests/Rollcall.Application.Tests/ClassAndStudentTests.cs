using Microsoft.EntityFrameworkCore;
using Rollcall.Application.Classes;
using Rollcall.Application.Common;
using Rollcall.Application.Students;
using Rollcall.Data.Entities;

namespace Rollcall.Application.Tests;

public sealed class ClassAndStudentTests : IDisposable
{
    private readonly TestDatabase db = new();

    public void Dispose() => db.Dispose();

    [Fact]
    public async Task New_class_gets_an_upper_case_section_and_tidy_fields()
    {
        var result = await db.Classes().CreateAsync(new ClassInput(10, " c ", "  Deniz   Uysal ", "b-106"));

        Assert.True(result.Success, result.Message);
        var saved = await db.Fresh().Classes.SingleAsync(c => c.Id == result.Value);
        Assert.Equal("C", saved.Section);
        Assert.Equal("Deniz Uysal", saved.AdvisorName);
        Assert.Equal("B-106", saved.Room);
    }

    [Theory]
    [InlineData("a")]
    [InlineData(" A ")]
    public async Task The_same_class_cannot_be_added_twice(string section)
    {
        var result = await db.Classes().CreateAsync(new ClassInput(9, section, null, null));

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Equal(nameof(ClassInput.Section), result.Field);
    }

    [Theory]
    [InlineData(0, "A")]
    [InlineData(13, "A")]
    [InlineData(9, "")]
    [InlineData(9, "ABC")]
    [InlineData(9, "1")]
    public async Task Class_input_is_checked(int grade, string section)
    {
        var result = await db.Classes().CreateAsync(new ClassInput(grade, section, null, null));

        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public void Turkish_letters_are_upper_cased_the_turkish_way()
    {
        Assert.Equal("İ", ClassService.NormalizeSection("i"));
    }

    [Fact]
    public async Task A_class_with_students_or_records_cannot_be_deleted()
    {
        db.AddStudent("Ayşe", "Yılmaz");
        db.AddExam(schoolClass: db.NineB);

        var withStudents = await db.Classes().DeleteAsync(db.NineA.Id);
        var withExams = await db.Classes().DeleteAsync(db.NineB.Id);

        Assert.Contains("1 öğrenci", withStudents.Message);
        Assert.Equal(ResultStatus.Invalid, withExams.Status);
        Assert.Equal(2, await db.Fresh().Classes.CountAsync());
    }

    [Fact]
    public async Task An_empty_class_can_be_deleted()
    {
        var created = await db.Classes().CreateAsync(new ClassInput(11, "A", null, null));

        var result = await db.Classes().DeleteAsync(created.Value);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task Class_list_counts_active_students_only()
    {
        db.AddStudent("Ayşe", "Yılmaz");
        db.AddStudent("Mehmet", "Kaya", active: false);

        var row = (await db.Classes().GetAllAsync()).Single(c => c.Id == db.NineA.Id);

        Assert.Equal(1, row.StudentCount);
        Assert.Equal("9-A", row.Name);
    }

    private StudentInput Input(string number = "2001", string first = "ayşe nur", string last = "YILDIRIM", string? email = null, int? classId = null) =>
        new(number, first, last, email, classId ?? db.NineA.Id);

    [Fact]
    public async Task Names_are_capitalised_the_turkish_way()
    {
        var result = await db.Students().CreateAsync(Input(first: "  ayşe   nur ", last: "IŞIK", email: " Ayse@Okul.Example.com "));

        var saved = await db.Fresh().Students.SingleAsync(s => s.Id == result.Value);
        Assert.Equal("Ayşe Nur", saved.FirstName);
        Assert.Equal("Işık", saved.LastName);
        Assert.Equal("ayse@okul.example.com", saved.Email);
    }

    [Fact]
    public async Task School_numbers_and_emails_are_unique()
    {
        var first = db.AddStudent("Ayşe", "Yılmaz", email: "ayse@okul.example.com");

        var sameNumber = await db.Students().CreateAsync(Input(number: first.StudentNumber));
        var sameEmail = await db.Students().CreateAsync(Input(email: "AYSE@okul.example.com"));

        Assert.Equal(nameof(StudentInput.StudentNumber), sameNumber.Field);
        Assert.Equal(nameof(StudentInput.Email), sameEmail.Field);
    }

    [Theory]
    [InlineData("12a", null)]
    [InlineData("", null)]
    [InlineData("12345678901", null)]
    [InlineData("2001", "ayse@")]
    [InlineData("2001", "ayse@okul")]
    public async Task Student_input_is_checked(string number, string? email)
    {
        var result = await db.Students().CreateAsync(Input(number: number, email: email));

        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task Search_matches_names_and_numbers_and_hides_former_students()
    {
        var ayse = db.AddStudent("Ayşe", "Yılmaz");
        db.AddStudent("Mehmet", "Kaya", db.NineB);
        db.AddStudent("Ayşegül", "Demir", active: false);

        var byName = await db.Students().ListAsync(new StudentQuery { Search = "ayş" });
        var byNumber = await db.Students().ListAsync(new StudentQuery { Search = ayse.StudentNumber });
        var byClass = await db.Students().ListAsync(new StudentQuery { ClassId = db.NineB.Id });
        var withFormer = await db.Students().ListAsync(new StudentQuery { Search = "ayş", IncludeInactive = true });

        Assert.Equal("Ayşe Yılmaz", Assert.Single(byName.Items).FullName);
        Assert.Single(byNumber.Items);
        Assert.Equal("Mehmet Kaya", Assert.Single(byClass.Items).FullName);
        Assert.Equal(2, withFormer.TotalCount);
    }

    [Fact]
    public async Task Next_number_follows_the_highest_one()
    {
        db.AddStudent("Ayşe", "Yılmaz");
        db.AddStudent("Mehmet", "Kaya");

        Assert.Equal("102", await db.Students().NextNumberAsync());
    }

    [Fact]
    public async Task Students_with_records_cannot_be_deleted_but_can_leave()
    {
        var student = db.AddStudent("Ayşe", "Yılmaz");
        db.AddAttendance(student, db.Today, AttendanceStatus.Absent);

        var delete = await db.Students().DeleteAsync(student.Id);
        var leave = await db.Students().UpdateAsync(student.Id, Input(number: student.StudentNumber, first: "Ayşe", last: "Yılmaz") with { IsActive = false });

        Assert.Equal(ResultStatus.Invalid, delete.Status);
        Assert.True(leave.Success, leave.Message);
        Assert.False((await db.Fresh().Students.SingleAsync()).IsActive);
    }

    [Fact]
    public async Task Students_without_records_can_be_deleted()
    {
        var student = db.AddStudent("Ayşe", "Yılmaz");

        var result = await db.Students().DeleteAsync(student.Id);

        Assert.True(result.Success);
        Assert.Equal(0, await db.Fresh().Students.CountAsync());
    }
}
