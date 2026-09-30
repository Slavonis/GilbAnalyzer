// example_switch.js — воспроизведение примера из методички:
// разветвляющийся алгоритм вычисления функции Y с оператором
// множественного выбора на n = 5 ветвей (рис. 2/3).
// Ожидается: CL = 4 (n − 1), CLI = 3 (n − 2).

function computeY(x) {
    var y;
    switch (true) {
        case x < 0:
            y = 0;
            break;
        case x === 0:
            y = 1;
            break;
        case x < 0.5:
            y = 2;
            break;
        case x < 1:
            y = 3;
            break;
        default:
            y = 4;
    }
    return y;
}
