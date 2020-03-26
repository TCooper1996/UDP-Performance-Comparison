names = ["1kb", "1mb", "100mb"]
lines = [1, 1024, 1024*100]
for file in range(3):
    f = open(names[file] + ".txt", 'w')
    for line in range(lines[file]):
        f.write("Here are some words followed by 981 dashes" + "-"*981 + '\n')
        
