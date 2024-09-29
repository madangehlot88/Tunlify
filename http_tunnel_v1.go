package main

import (
	"flag"
	"fmt"
	"io"
	"log"
	"net"
	"os"
)

var (
	serverIP   string
	serverPort string
	localIP    string
	localPort  string
	logFile    string
)

func init() {
	flag.StringVar(&serverIP, "server-ip", "", "Remote server IP address")
	flag.StringVar(&serverPort, "server-port", "3742", "Remote server port")
	flag.StringVar(&localIP, "local-ip", "192.168.1.1", "Local OpenWRT IP address")
	flag.StringVar(&localPort, "local-port", "80", "Local OpenWRT port")
	flag.StringVar(&logFile, "log", "tunnel_client.log", "Log file path")
}

func main() {
	flag.Parse()

	if serverIP == "" {
		log.Fatal("Server IP is required. Use -server-ip flag.")
	}

	f, err := os.OpenFile(logFile, os.O_RDWR|os.O_CREATE|os.O_APPEND, 0666)
	if err != nil {
		log.Fatalf("Error opening log file: %v", err)
	}
	defer f.Close()
	log.SetOutput(f)

	for {
		err := runTunnel()
		if err != nil {
			log.Printf("Tunnel error: %v. Retrying...", err)
		}
	}
}

func runTunnel() error {
	serverConn, err := net.Dial("tcp", fmt.Sprintf("%s:%s", serverIP, serverPort))
	if err != nil {
		return fmt.Errorf("failed to connect to server: %v", err)
	}
	defer serverConn.Close()

	log.Printf("Connected to server %s:%s", serverIP, serverPort)

	localConn, err := net.Dial("tcp", fmt.Sprintf("%s:%s", localIP, localPort))
	if err != nil {
		return fmt.Errorf("failed to connect to local service: %v", err)
	}
	defer localConn.Close()

	log.Printf("Connected to local service %s:%s", localIP, localPort)

	go func() {
		_, err := io.Copy(serverConn, localConn)
		if err != nil {
			log.Printf("Error copying local to server: %v", err)
		}
	}()

	_, err = io.Copy(localConn, serverConn)
	if err != nil {
		return fmt.Errorf("error copying server to local: %v", err)
	}

	return nil
}