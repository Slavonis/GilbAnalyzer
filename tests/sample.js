// sample.js — подготовленная программа на JavaScript для анализа метрик Джилба.
// Содержит ВСЕ операторы цикла языка (for, for-of, for-in, while, do-while),
// операторы ветвления (if/else) и оператор множественного выбора (switch).

function analyzeNumbers(numbers) {
    var stats = { positive: 0, negative: 0, zero: 0 };

    // Цикл for
    for (var i = 0; i < numbers.length; i++) {
        var value = numbers[i];
        // Ветвление if / else-if / else
        if (value > 0) {
            stats.positive = stats.positive + 1;
        } else if (value < 0) {
            stats.negative = stats.negative + 1;
        } else {
            stats.zero = stats.zero + 1;
        }
    }

    // Цикл for-of
    var sum = 0;
    for (var num of numbers) {
        sum += num;
    }

    // Цикл for-in
    var report = "";
    for (var key in stats) {
        report = report + key + "=" + stats[key] + "; ";
    }

    // Цикл while
    var count = numbers.length;
    var doubled = [];
    while (count > 0) {
        count = count - 1;
        doubled.push(numbers[count] * 2);
    }

    // Цикл do-while
    var attempts = 0;
    do {
        attempts = attempts + 1;
    } while (attempts < 3);

    return { stats: stats, sum: sum, report: report, doubled: doubled };
}

// Оператор множественного выбора switch (классификация по среднему значению).
function classify(avg) {
    var grade;
    for (var i = 0; i < numbers.length; i++) {
        switch (true) {
            case avg >= 90:
                grade = "A";
                break;
            case avg >= 75:
                grade = "B";
                break;
            case avg >= 60:
                grade = "C";
                break;
            default:
                grade = "F";
        }
    }
    return grade;
}

var data = [5, -3, 0, 8, -1, 4];
var result = analyzeNumbers(data);
var average = result.sum / data.length;

// Тернарный оператор ?:
var label = (average >= 0) ? "неотрицательное" : "отрицательное";

console.log(result.report);
console.log("Среднее: " + average + " (" + label + "), оценка: " + classify(average));
