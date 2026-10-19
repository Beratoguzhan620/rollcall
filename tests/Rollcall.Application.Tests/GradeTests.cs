using Microsoft.EntityFrameworkCore;
using Rollcall.Application.Common;
using Rollcall.Application.Grades;

namespace Rollcall.Application.Tests;

public sealed class GradeTests : IDisposable
{
    private readonly TestDatabase db = new();

    public void Dispose() => db.Dispose();

    [Fact]
    public async Task Exams_need_a_title_class_and_course_and_a_unique_name()
    {
        var ok = await db.Grades().CreateExamAsync(new ExamInput(db.NineA.Id, 2, "  1.  yazılı ", db.Today));
        var duplicate = await db.Grades().CreateExamAsync(new ExamInput(db.NineA.Id, 2, "1. yazılı", db.Today));
        var otherClass = await db.Grades().CreateExamAsync(new ExamInput(db.NineB.Id, 2, "1. yazılı", db.Today));
        var noCourse = await db.Grades().CreateExamAsync(new ExamInput(db.NineA.Id, 99, "Proje", db.Today));
        var noTitle = await db.Grades().CreateExamAsync(new ExamInput(db.NineA.Id, 2, " ", db.Today));

        Assert.True(ok.Success, ok.Message);
        Assert.Equal("1. yazılı", (await db.Fresh().Exams.FindAsync(ok.Value))!.Title);
        Assert.Equal(nameof(ExamInput.Title), duplicate.Field);
        Assert.True(otherClass.Success);
        Assert.Equal(nameof(ExamInput.CourseId), noCourse.Field);
        Assert.Equal(nameof(ExamInput.Title), noTitle.Field);
    }

    [Fact]
    public async Task Scores_are_added_changed_and_removed_in_one_save()
    {
        var exam = db.AddExam();
        var ali = db.AddStudent("Ali", "Yılmaz");
        var ece = db.AddStudent("Ece", "Kaya");
        var can = db.AddStudent("Can", "Demir");
        db.AddGrade(exam, ece, 40);
        db.AddGrade(exam, can, 90);

        var result = await db.Grades().SaveScoresAsync(exam.Id, new Dictionary<int, decimal?>
        {
            [ali.Id] = 72.5m,
            [ece.Id] = 55m,
            [can.Id] = null,
        });

        Assert.True(result.Success, result.Message);
        var grades = await db.Fresh().Grades.ToDictionaryAsync(g => g.StudentId, g => g.Score);
        Assert.Equal(2, grades.Count);
        Assert.Equal(72.5m, grades[ali.Id]);
        Assert.Equal(55m, grades[ece.Id]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100.5)]
    [InlineData(70.125)]
    public async Task Out_of_range_or_too_precise_scores_are_refused(double score)
    {
        var exam = db.AddExam();
        var ali = db.AddStudent("Ali", "Yılmaz");

        var result = await db.Grades().SaveScoresAsync(exam.Id, new Dictionary<int, decimal?> { [ali.Id] = (decimal)score });

        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task Students_of_another_class_cannot_be_graded()
    {
        var exam = db.AddExam();
        var other = db.AddStudent("Başka", "Sınıf", db.NineB);

        var result = await db.Grades().SaveScoresAsync(exam.Id, new Dictionary<int, decimal?> { [other.Id] = 80 });

        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task Exam_list_shows_progress_and_average()
    {
        var exam = db.AddExam();
        var ali = db.AddStudent("Ali", "Yılmaz");
        var ece = db.AddStudent("Ece", "Kaya");
        db.AddStudent("Can", "Demir");
        db.AddGrade(exam, ali, 70);
        db.AddGrade(exam, ece, 85.5m);

        var row = Assert.Single(await db.Grades().GetExamsAsync(db.NineA.Id));

        Assert.Equal(2, row.GradedCount);
        Assert.Equal(3, row.StudentCount);
        Assert.Equal(77.75m, row.Average);
        Assert.Equal("Matematik", row.CourseName);
    }

    [Fact]
    public async Task Overall_average_weighs_each_course_equally()
    {
        var ali = db.AddStudent("Ali", "Yılmaz");
        var math1 = db.AddExam(2, "1. yazılı");
        var math2 = db.AddExam(2, "2. yazılı");
        var math3 = db.AddExam(2, "3. yazılı");
        var physics = db.AddExam(3, "1. yazılı");
        db.AddGrade(math1, ali, 100);
        db.AddGrade(math2, ali, 100);
        db.AddGrade(math3, ali, 100);
        db.AddGrade(physics, ali, 40);

        var grades = await db.Grades().GetStudentAsync(ali.Id);

        Assert.Equal(100m, grades.Courses.Single(c => c.CourseName == "Matematik").Average);
        Assert.Equal(70m, grades.Overall);
    }

    [Fact]
    public async Task Deleting_an_exam_removes_its_grades()
    {
        var exam = db.AddExam();
        db.AddGrade(exam, db.AddStudent("Ali", "Yılmaz"), 80);

        var result = await db.Grades().DeleteExamAsync(exam.Id);

        Assert.True(result.Success);
        Assert.Equal(0, await db.Fresh().Grades.CountAsync());
    }

    [Fact]
    public async Task Grade_book_has_every_active_student_and_graded_exam()
    {
        var ali = db.AddStudent("Ali", "Yılmaz");
        db.AddStudent("Ece", "Kaya");
        var graded = db.AddExam(2, "1. yazılı");
        db.AddExam(2, "2. yazılı");
        db.AddGrade(graded, ali, 64);

        var book = await db.Grades().GetGradeBookAsync(db.NineA.Id);

        Assert.Equal(graded.Id, Assert.Single(book.Exams).Id);
        Assert.Equal(2, book.Rows.Count);
        Assert.Equal(64m, book.Rows.Single(r => r.FullName == "Ali Yılmaz").Scores[graded.Id]);
        Assert.Empty(book.Rows.Single(r => r.FullName == "Ece Kaya").Scores);
    }

    [Fact]
    public async Task The_database_refuses_scores_outside_0_to_100()
    {
        var exam = db.AddExam();
        var ali = db.AddStudent("Ali", "Yılmaz");

        Assert.ThrowsAny<DbUpdateException>(() => db.AddGrade(exam, ali, 120));
        await Task.CompletedTask;
    }
}
